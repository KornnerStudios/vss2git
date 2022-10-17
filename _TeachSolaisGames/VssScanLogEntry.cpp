/////////////////////////////////////////////////////////////////////////////
//
//	File: VssScanLogEntry.cpp
//
//	$Header:$
//
/////////////////////////////////////////////////////////////////////////////


#include "Common.h"
#include "VssScanLogEntry.h"
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
VssScanLogEntry::VssScanLogEntry(void)
{
}


/////////////////////////////////////////////////////////////////////////////
//
//	Scan()
//
void VssScanLogEntry::Scan(BinaryReader &reader)
{
	// Temporary holder for "aaaaaaaa" name strings.
	char dbname[10];

	m_FileName[0]       = '\0';
	m_FileReference     = -1;
	m_BranchReference   = -1;

	m_PreviousOffset = reader.Read32();
	m_Opcode         = reader.Read16();
	m_VersionNumber  = reader.Read16();
	m_Timestamp      = reader.Read32();

	reader.ReadData(m_Username, 32);
	reader.ReadData(m_Label, 32);

	m_CommentOffset = reader.Read32();
	m_LabelOffset   = reader.Read32();
	m_CommentLength = reader.Read16();
	m_LabelLength   = reader.Read16();

	if (VssOpcode_SharedFile == m_Opcode) {
		reader.ReadData(m_DatabasePath, 260);
	}

	if (VssOpcode_CheckedInFile == m_Opcode) {
		m_DifferenceOffset = reader.Read32();

		// Next 32 bits are always zero in the database that was tested.
		u32 zero = reader.Read32();
		if (0 != zero) {
			printf("error: non-zero check-in value: %d\n", zero);
		}

		reader.ReadData(m_DatabasePath, 260);

		// Stop here.
		// None of the remaining fields are present in a check-in operation.

		return;
	}

	// 0x0000 == m_FileName is name of a file
	// 0x0001 == m_FileName is name of a directory (project)
	// 0x033C == m_FileName is an empty string
	u16 nameFlags = reader.Read16();

	reader.ReadData(m_FileName, 34);

	if ((0 != nameFlags) && (1 != nameFlags) && (0x033C != nameFlags)) {
		printf("error: invalid name flag 0x%04X \"%s\"\n", nameFlags, m_FileName);
	}

	m_NamesFileOffset = reader.Read32();

	if ((VssOpcode_RenamedProject == m_Opcode) ||
		(VssOpcode_RenamedFile    == m_Opcode))
	{
		// 0x0000 == m_NewFileName is name of a file
		// 0x0001 == m_NewFileName is name of a directory (project)
		// 0x033C == m_NewFileName is an empty string
		u16 altNameFlags = reader.Read16();

		reader.ReadData(m_NewFileName, 34);

		if ((0 != altNameFlags) && (1 != altNameFlags) && (0x033C != altNameFlags)) {
			printf("error: invalid alt name flag 0x%04X \"%s\"\n", altNameFlags, m_NewFileName);
		}

		m_NewFileNamesOffset = reader.Read32();

		reader.ReadData(dbname, 10);

		m_FileReference = VssNameToNumber(dbname);
	}
	else if (VssOpcode_SharedFile == m_Opcode) {
		reader.Read16();
		reader.Read16();
		reader.Read16();
		reader.ReadData(dbname, 10);

		m_FileReference = VssNameToNumber(dbname);
	}
	else {
		reader.ReadData(dbname, 10);

		m_FileReference = VssNameToNumber(dbname);
	}

	if (VssOpcode_BranchedFile == m_Opcode) {
		reader.ReadData(dbname, 10);

		m_BranchReference = VssNameToNumber(dbname);
	}

//	char timestring[128];
//	strcpy(timestring, asctime(gmtime((time_t*)&m_Timestamp)));
}


/////////////////////////////////////////////////////////////////////////////
//
//	Dump()
//
void VssScanLogEntry::Dump(void)
{
	switch (m_Opcode) {
		case VssOpcode_Labeled:
//			printf("labeled: %s\n", m_Label);
			break;

		// This is the first log entry in a project, containing info about
		// its creation.
		case VssOpcode_CreatedProject:
//			printf("created project: %s, id = %d\n", m_FileName, m_FileReference);
			break;

		case VssOpcode_AddedProject:
//			printf("added project: %s, id = %d\n", m_FileName, m_FileReference);
			break;

		case VssOpcode_AddedFile:
//			printf("added file: %s, id = %d\n", m_FileName, m_FileReference);
			break;

		case VssOpcode_DestroyedProject:
//			printf("destroyed project: %s, id = %d\n", m_FileName, m_FileReference);
			break;

		// Destroyed files may have an empty string for the database file
		// name.  If this happens, the file is probably still stored in the
		// database, orphaned and unused.  Traversing the tree will never
		// visit this file, which will show up as "not visited" during the
		// final scan by LookForUnused().
		case VssOpcode_DestroyedFile:
//			printf("destroyed file: %s, id = %d\n", m_FileName, m_FileReference);
			break;

		case VssOpcode_DeletedProject:
//			printf("delete project: %s, id = %d\n", m_FileName, m_FileReference);
			break;

		case VssOpcode_DeletedFile:
//			printf("delete file: %s, id = %d\n", m_FileName, m_FileReference);
			break;

		case VssOpcode_RecoveredFile:
//			printf("recovered file: %s, id = %d\n", m_FileName, m_FileReference);
			break;

		case VssOpcode_RenamedProject:
//			printf("renamed project: %s, %s, id = %d\n", m_FileName, m_NewFileName, m_FileReference);
			break;

		case VssOpcode_RenamedFile:
//			printf("renamed file: %s, %s, id = %d\n", m_FileName, m_NewFileName, m_FileReference);
			break;

		case VssOpcode_SharedFile:
//			printf("shared file: %s, %s, id = %d\n", m_FileName, m_DatabasePath, m_FileReference);
			break;

		case VssOpcode_BranchedFile:
//			printf("branched file: %s, id = %d, branch id = %d\n", m_FileName, m_FileReference, m_BranchReference);
			break;

		// This only appears in the info file for files, not projects.
		case VssOpcode_CreatedFile:
//			printf("created file: %s, id = %d\n", m_FileName, m_FileReference);
			break;

		// This only appears in the info file for files, not projects.
		case VssOpcode_CheckedInFile:
//			printf("checked in: %s\n", m_DatabasePath);
			break;

		default:
//			printf("error: unknown opcode %d\n", m_Opcode);
			break;
	}
}
