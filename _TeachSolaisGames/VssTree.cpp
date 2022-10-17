/////////////////////////////////////////////////////////////////////////////
//
//	File: VssTree.cpp
//
//	$Header:$
//
/////////////////////////////////////////////////////////////////////////////


#include "Common.h"
#include "CRC32.h"
#include "VssScanCheckout.h"
#include "VssScanChild.h"
#include "VssScanHeader.h"
#include "VssScanLogEntry.h"
#include "VssScanParent.h"
#include "VssTree.h"
#include "VssUtils.h"


#ifdef _DEBUG
#define new DEBUG_NEW
#undef THIS_FILE
static char THIS_FILE[] = __FILE__;
#endif


/////////////////////////////////////////////////////////////////////////////
//
//	constructor
//
VssTree::VssTree(void)
	:	m_NodeCount(0),
		m_MaxNodes(4096),
		m_pNodes(NULL),
		m_InfoCount(0),
		m_DataCount(0),
		m_MemorySize(0)
{
	m_pNodes = new VssNode_t[m_MaxNodes];

	memset(m_pNodes, 0, m_MaxNodes * sizeof(VssNode_t));
}


/////////////////////////////////////////////////////////////////////////////
//
//	destructor
//
VssTree::~VssTree(void)
{
	Free();

	SafeDeleteArray(m_pNodes);
}


/////////////////////////////////////////////////////////////////////////////
//
//	Import()
//
void VssTree::Free(void)
{
	for (u32 i = 0; i < m_NodeCount; ++i) {
		SafeDeleteArray(m_pNodes[i].pInfo);
		SafeDeleteArray(m_pNodes[i].pData);
		m_pNodes[i].Type = 0;
	}

	m_NodeCount  = 0;
	m_InfoCount  = 0;
	m_DataCount  = 0;
	m_MemorySize = 0;
}


/////////////////////////////////////////////////////////////////////////////
//
//	Import()
//
bool VssTree::Import(wchar_t path[])
{
	Free();

	wchar_t dirname[MAX_PATH];

	u32 pathLen = u32(wcslen(path));

	if ((pathLen + 16) > MAX_PATH) {
		return false;
	}

	wcscpy(dirname, path);

	if ((pathLen > 0) && ('\\' != dirname[pathLen])) {
		dirname[pathLen++] = '\\';
		dirname[pathLen]   = '\0';
	}

	wcscat(dirname, L"data\\");

	pathLen += 5;

	for (wchar_t dirNum = 'a'; dirNum <= 'z'; ++dirNum) {
		dirname[pathLen  ] = dirNum;
		dirname[pathLen+1] = '\\';
		dirname[pathLen+2] = '\0';

		if (false == ImportDir(dirname)) {
			return false;
		}
	}

	printf("import successful:\n");
	printf("   info files:  %d\n", m_InfoCount);
	printf("   data files:  %d\n", m_DataCount);
	printf("   memory size: %d\n", m_MemorySize);

	if (false == AssembleDirectoryLinks(0, 0, NULL)) {
		return false;
	}

	return LookForUnused();
}


/////////////////////////////////////////////////////////////////////////////
//
//	ImportDir()
//
bool VssTree::ImportDir(wchar_t dirname[])
{
	wchar_t searchpath[MAX_PATH];
	wchar_t filename[MAX_PATH];

	wcscpy(searchpath, dirname);
	wcscat(searchpath, L"*");

	WIN32_FIND_DATA data;

	HANDLE hFind = FindFirstFile(searchpath, &data);

	if (INVALID_HANDLE_VALUE != hFind) {
		do {
			if (0 == (data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)) {
				if (IsVssInfoFileName(data.cFileName)) {
					wcscpy(filename, dirname);
					wcscat(filename, data.cFileName);
					if (false == ImportFile(filename, VssNameToNumber(data.cFileName), true)) {
						return false;
					}
				}
				else if (IsVssDataFileName(data.cFileName)) {
					wcscpy(filename, dirname);
					wcscat(filename, data.cFileName);
					if (false == ImportFile(filename, VssNameToNumber(data.cFileName), false)) {
						return false;
					}
				}
			}
		} while (FindNextFile(hFind, &data));

		FindClose(hFind);
	}

	return true;
}


/////////////////////////////////////////////////////////////////////////////
//
//	ImportFile()
//
bool VssTree::ImportFile(wchar_t filename[], u32 index, bool isInfo)
{
	if (index >= m_MaxNodes) {
		// FIXME: could grow the array here instead of erroring out
		wprintf(L"ImportFile error: index (%d) out of range\n", index);
		return false;
	}

	if (m_NodeCount <= index) {
		m_NodeCount = index + 1;
	}

	if (isInfo) {
		if (NULL != m_pNodes[index].pInfo) {
			wprintf(L"ImportFile error: duplicate info file %s\n", filename);
			return false;
		}
	}
	else {
		if (NULL != m_pNodes[index].pData) {
			wprintf(L"ImportFile error: duplicate data file %s\n", filename);
			return false;
		}
	}

	FILE *pFile = _wfopen(filename, L"rb");
	if (NULL == pFile) {
		wprintf(L"ImportFile error: cannot open file %s\n", filename);
		return false;
	}

	fseek(pFile, 0, SEEK_END);
	u32 byteCount = ftell(pFile);
	fseek(pFile, 0, SEEK_SET);

	u08 *pData = new u08[byteCount];

	u32 readSize = u32(fread(pData, 1, byteCount, pFile));

	fclose(pFile);

	if (readSize != byteCount) {
		wprintf(L"ImportFile error: read %d of %d bytes, file %s\n", readSize, byteCount, filename);
		SafeDeleteArray(pData);
		return false;
	}

	if (isInfo) {
		m_pNodes[index].InfoSize = byteCount;
		m_pNodes[index].pInfo    = pData;

		m_InfoCount  += 1;
		m_MemorySize += byteCount;
	}
	else {
		m_pNodes[index].DataSize = byteCount;
		m_pNodes[index].pData    = pData;

		m_DataCount  += 1;
		m_MemorySize += byteCount;
	}

	return true;
}


void PrintComment(BinaryReader &reader, u32 size)
{
	char *pBuffer = new char[size+1];
	reader.ReadData(pBuffer, size);

	// Comments appear to always be terminated by '\0', but we'll explicitly
	// terminate the string to be safe.
	pBuffer[size] = '\0';
//	printf("comment: \"%s\"\n", pBuffer);
	delete [] pBuffer;
}


/////////////////////////////////////////////////////////////////////////////
//
//	ApplyDifferenceData()
//
//	This will apply a set of differences to a file, converting it into the
//	previous version of the file.  This assumes that a copy of the current
//	file is stored in <pNewFile>.  It will use the difference data from
//	the byte stream to transform <pNewFile> into the previous version of the
//	file, which is written out to <pFile>.
//
void ApplyDifferenceData(FILE *pFile, u08 *pNewFile, BinaryReader &reader)
{
	u16 opcode = 0;

	do {
		// Read the opcode that indicates whether to insert, copy, or stop.
		opcode = reader.Read16();

		// Next 16 bits is junk.  Ignore it.
		reader.Read16();

		// Then there are always a pair of values for offset and count, even
		// when they're not needed.
		u32 offset = reader.Read32();
		u32 count  = reader.Read32();

		// Insert <count> bytes from the data stream.
		if (0 == opcode) {
			fwrite(reader.CurrentAddress(), 1, count, pFile);
			reader.Skip(count);
		}

		// Copy <count> bytes from the <pNewFile> array.
		else if (1 == opcode) {
			fwrite(pNewFile + offset, 1, count, pFile);
		}

		else {
			// The only other value is 2, which indicates the end of the
			// difference data.
		}

	} while (opcode < 2);
}


/////////////////////////////////////////////////////////////////////////////
//
//	AssembleDirectoryLinks()
//
bool VssTree::AssembleDirectoryLinks(u32 index, u32 depth, char path[])
{
	if ((NULL == m_pNodes[index].pInfo) || (NULL == m_pNodes[index].pData)) {
		printf("error: node %d does not exist\n", index);
		return false;
	}

	BinaryReader reader;
	reader.Initialize(m_pNodes[index].pInfo, m_pNodes[index].InfoSize);

	VssScanHeader header;

	if (false == header.Scan(reader)) {
		return false;
	}

	char debugname[16];
	VssNumberToName(index, debugname);

	char pathname[256];
	if (NULL == path) {
		pathname[0] = '\0';
	}
	else {
		strcpy(pathname, path);
		strcat(pathname, "\\");
	}
	strcat(pathname, header.m_Name);

	printf("%s (%d) %d bytes\n", pathname, index, m_pNodes[index].InfoSize);

	// If the type has been set to something non-zero, we have already
	// processed this file.
	if (0 != m_pNodes[index].Type) {
//		printf("revisiting\n");
		return true;
	}

	m_pNodes[index].Type = header.m_Type;

	VssScanLogEntry logEntry;

	if (VssType_Project == m_pNodes[index].Type) {
		// This is the path to the parent project within VSS.  This will start
		// with "$/".  The exception is the root project, for which this will
		// be an empty string.
		u08 vsspath[260];
		reader.ReadData(vsspath, 260);
//		printf("%s\n", vsspath);

		// The name of the parent project in "aaaaaaaa" format.  This is an
		// empty string for the root of the data base.
		char parentName[12];
		reader.ReadData(parentName, 12);

		// Do this for every project, except the root project, which will
		// always contain an empty string.
		if ('\0' != parentName[0]) {
			// Record the index of the parent for reference.
			m_pNodes[index].ParentID = VssNameToNumber(parentName);

			if (m_pNodes[index].ParentID >= m_NodeCount) {
				printf("error: invalid parent name \"%s\"\n", parentName);
				return false;
			}
		}

//		printf("parent file %s\n", parentName);

		// Number of child entries in the associated data file.
		u16 childCount = reader.Read16();

		// How many of the children are projects?
		// Subtract projectCount from childCount to find out how many files
		// are stored in this directory (note that this includes all files
		// and projects that have been deleted, but whose history is still
		// stored in the database).
		u16 projectCount = reader.Read16();

		if (projectCount > childCount) {
			printf("error: projectCount %d > childCount %d\n", projectCount, childCount);
			return false;
		}

		while (reader.Offset() < reader.DataSize()) {
			u32 chunkSize  = reader.Read32();
			u16 chunkID    = reader.Read16();
			u16 crc        = reader.Read16();
			u32 baseOffset = reader.Offset();

			if ((0 != crc) && (crc != reader.ComputeCRC(chunkSize))) {
				printf("bad CRC\n");
				return false;
			}

			switch (chunkID) {
				// Note that comments are not only from modifying a directory,
				// but also from labels -- the comment for a label is not
				// stored as part of the label.
				// And some operations will always add a comment block, even
				// when the comment is an empty string.
				case VssMarker_Comment:
					PrintComment(reader, chunkSize);
					break;

				case VssMarker_LogEntry:
					logEntry.Scan(reader);
					logEntry.Dump();
					break;

				default:
					printf("error: unknown chunk %04X\n", chunkID);
					break;
			}

			reader.SetOffset(baseOffset + chunkSize);
		}
	}
	else {
		// 0x01 = checked out
		// 0x02 = binary data
		// 0x20 = unknown
		// 0x40 = checked out
		//
		// For whatever reason, if a file is checked out, both 0x40 and 0x01
		// are both set.
		//
		u16 flags = reader.Read16();

		// If this file has been shared from a pre-existing file, this will
		// be the name of the file with which it shares.  If this was not
		// shared, this will be an empty string.  This string is still valid
		// after the file has been branched.
		char sharedReference[10];
		reader.ReadData(sharedReference, 10);

		u32 lastBranchOffset = reader.Read32();
		u32 lastParentOffset = reader.Read32();

		// Number of branch chunks that are stored in the file's change log.
		u16 branchCount = reader.Read16();

		// This is the number of valid parent chunks.  A new file starts off
		// with one parent chunk.  Each time the file is shared, a new parent
		// chunk is appended to the file.  Parent chunks are never deleted
		// from the file.  However, if a file is branched, the associated
		// parent chunk has the parent name zeroed out.
		//
		// This field stores the number of currently valid parent chunks
		// that are in the file.  Chunks related to branched file are not
		// part of this count.
		//
		// Parent chunks have the offset of the previous parent.  Could try
		// to traverse backwards from the last parent, counting the number
		// that are actually in the list.  Maybe that is what it adds up to.
		//
		u16 parentCount = reader.Read16();

		u32 checkoutActive   = reader.Read32();
		u32 checkoutInactive = reader.Read32();

		// This is the offset of the checkout chunk.  If the file is checked
		// out, checkoutActive will be 0x01A0, which is the offset of the
		// checkout chunk.  Can this be different if there is more than one
		// checkout chunk in the file?
		//
		// If the file is not checked out, checkoutInactive will be 0x01A0.
		//
		if (0 == checkoutActive) {
			if (0x01A0 != checkoutInactive) {
				printf("unexpected checkoutInactive: %08X\n", checkoutInactive);
			}
		}
		else {
			if (0 != checkoutInactive) {
				printf("unexpected checkoutInactive: %08X\n", checkoutInactive);
			}
			if (0x01A0 != checkoutActive) {
				printf("unexpected checkoutActive: %08X\n", checkoutActive);
			}
		}

		// This is a 32-bit CRC of the current data file.  Note that this uses
		// CRC logic that starts XORing from 0 instead of -1.
		u32 dataCRC = reader.Read32();

		u32 computedCRC = VssCrc32(m_pNodes[index].pData, m_pNodes[index].DataSize);
		if (dataCRC != computedCRC) {
			printf("CRC mismatch, corrupted data: 0x%08X != 0x%08X\n", dataCRC, computedCRC);
		}

		u08 zeroes1[8];
		reader.ReadData(zeroes1, 8);

		for (int i = 0; i < 8; ++i) {
			if (0 != zeroes1[i]) {
				printf("non-zero1 [%d] = %02X\n", i, zeroes1[i]);
			}
		}

		// Timestamps from the file when it was checked in.
		u32 lastCheckinTime  = reader.Read32();
		u32 fileModifiedTime = reader.Read32();
		u32 fileCreationTime = reader.Read32();

		// This is random, uninitialized junk.  Frequently composed from
		// pieces of source code that was being checked in.
		u08 randomJunk[16];
		reader.ReadData(randomJunk, 16);

		// Long string of data that is initialized to all zeroes.
		u08 zeroes2[200];
		reader.ReadData(zeroes2, 200);

		for (int i = 0; i < 200; ++i) {
			if (0 != zeroes2[i]) {
				printf("non-zero2 [%d] = %02X\n", i, zeroes2[i]);
			}
		}

		u16 itemCount    = reader.Read16();
		u16 projectCount = reader.Read16();

		if (projectCount > itemCount) {
			printf("error: projectCount > itemCount, %d > %d\n", projectCount, itemCount);
		}

		u16 branchNum = 0;
		u16 parentNum = 0;

		while (reader.Offset() < reader.DataSize()) {
			u32 chunkSize   = reader.Read32();
			u16 chunkID     = reader.Read16();
			u16 crc         = reader.Read16();
			u32 baseOffset  = reader.Offset();

			if ((0 != crc) && (crc != reader.ComputeCRC(chunkSize))) {
				printf("bad CRC\n");
				return false;
			}

			switch (chunkID) {
				case VssMarker_BranchFile:
					++branchNum;
					{
						u32 previousOffset = reader.Read32();
						char dbname[10];
						reader.ReadData(dbname, 10);
//						printf("%04X: branch file: %d (%d)\n", baseOffset, VssNameToNumber(dbname), previousOffset);

						if (0 != previousOffset) {
							// This indicates the position of the previous
							// branch chunk.  It will only be non-zero if a
							// file was branched multiple times.
						}
					}
					break;

				case VssMarker_CheckOut:
					{
						VssScanCheckout checkout;
						checkout.Scan(reader);
//						checkout.Dump();
					}
					break;

				case VssMarker_Comment:
					PrintComment(reader, chunkSize);
					break;

				case VssMarker_Difference:
					// This is the change data that indicates how the current
					// version of a file needs to be changed to convert it into
					// the previous version.  Those changes cannot be applied
					// here.  Applying the changes requires walking backwards
					// through the history of the file.  The data file for this
					// node is the most recent version of the file.  To reverse
					// the changes, the code needs to start with the current
					// version of the file, walk backwards through the history,
					// apply each difference record to create the previous
					// version of the file.  The next difference record is then
					// applied to THAT version of the file to recreate the one
					// before it.  Applying all of the changes in reverse order
					// will result in the original file that was checked in (or
					// the copy of the file at the time the file was branched,
					// in the case of branching).
					//
					// ApplyDifferenceData(, , reader);
					break;

				case VssMarker_LogEntry:
					logEntry.Scan(reader);
//					logEntry.Dump();
					break;

				case VssMarker_ParentFolder:
					{
						VssScanParent parent;
						parent.Scan(reader);
//						parent.Dump();
						if (parent.m_ParentIndex >= 0) {
							++parentNum;
						}
					}
					break;

				default:
					printf("error: unknown chunk %04X\n", chunkID);
					break;
			}

			reader.SetOffset(baseOffset + chunkSize);
		}

		if (branchNum != branchCount) {
			printf("error: branch count does not match expected value: %d != %d\n", branchCount, branchNum);
		}

		if (parentCount != parentNum) {
			printf("error: parent count does not match number of entries still in use: %d != %d\n", parentCount, parentNum);
		}
	}

/*
	for (u32 i = 0; i < depth; ++i) {
		printf("    ");
	}

	printf("%s\n", reader.m_Name);
*/
	if (VssType_Project == m_pNodes[index].Type) {
		VssScanChild childScan;
		BinaryReader reader;
		reader.Initialize(m_pNodes[index].pData, m_pNodes[index].DataSize);

		while (reader.Offset() < reader.DataSize()) {
			u32 chunkSize  = reader.Read32();
			u16 chunkID    = reader.Read16();
			u16 crc        = reader.Read16();
			u32 baseOffset = reader.Offset();

			if ((0 != crc) && (crc != reader.ComputeCRC(chunkSize))) {
				printf("bad CRC\n");
				return false;
			}

			switch (chunkID) {
				case VssMarker_Child:
					childScan.Scan(reader);
//					childScan.Dump();
//					printf("project: %s\\%s, %s\n", pathname, childScan.m_Name, childScan.m_DBName);
					AssembleDirectoryLinks(VssNameToNumber(childScan.m_DBName), depth + 1, pathname);
					break;

				default:
					printf("error: unknown chunk ID 0x%04X\n", chunkID);
			}

			reader.SetOffset(baseOffset + chunkSize);
		}
	}
	else {
	}

	return true;
}


/////////////////////////////////////////////////////////////////////////////
//
//	LookForUnused()
//
//	Every file that was visited will be marked as either a project or a file.
//	If the file type is not marked, the file was not visited.  These appear
//	to be orphaned files that are still in the database, but not referenced
//	by anything.  They may still be required for historical purposes, when
//	getting the state of the project from a point in the past.
//
bool VssTree::LookForUnused(void)
{
	u32 notAllocated = 0;
	u32 notVisited   = 0;

	for (u32 i = 0; i < m_NodeCount; ++i) {
		// Unallocated entries indicate files that have been removed from
		// the database.  This occurs when destroying all projects that
		// reference a particular file -- once all references to the file
		// have been destroyed, the file will be removed from the database.
		if (NULL == m_pNodes[i].pInfo) {
			++notAllocated;
		}

		// If the type is still zero, the file is not currently used by
		// any project in the database.  These are either orphaned files,
		// or files that have been deleted, but still exist in the project's
		// history.
		else if (0 == m_pNodes[i].Type) {
			++notVisited;
			char name[16];
			VssNumberToName(i, name);
			printf("skipped: %d %s\n", i, name);
		}
	}

	printf("not allocated: %d\n", notAllocated);
	printf("not visited:   %d\n", notVisited);

	return true;
}





