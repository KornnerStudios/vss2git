/////////////////////////////////////////////////////////////////////////////
//
//	File: VssScanHeader.cpp
//
//	$Header:$
//
//
//	This code will scan the header chunk at the start of an info file (those
//	files with the "aaaaaaaa" format.  (Files with a ".a" or ".b" extension
//	are data files, the format of which depends upon whether this is a file
//	or a project.
//
/////////////////////////////////////////////////////////////////////////////


#include "Common.h"
#include "VssScanHeader.h"


#ifdef _DEBUG
#define new DEBUG_NEW
#undef THIS_FILE
static char THIS_FILE[] = __FILE__;
#endif


/////////////////////////////////////////////////////////////////////////////
//
//	Scan()
//
bool VssScanHeader::Scan(BinaryReader &reader)
{
	// Every info file starts with this 20-byte string.  Do a case-sensitive
	// test to verify that this is indeed an info file.
	if (false == reader.TestBytes((u08*)"SourceSafe@Microsoft", 20)) {
		printf("not a source-safe file\n");
		return false;
	}

	reader.Skip(20);

	// The next 12 bytes are always zero, probably to pad the marker out
	// to a 32-byte boundary.
	for (u32 i = 0; i < 3; ++i) {
		if (0 != reader.Read32()) {
			printf("non-zero value\n");
			return false;
		}
	}

	// Is this a file or a directory ("project" in VSS's goofy nomenclature)?
	m_Type = reader.Read16();

	if ((VssType_File != m_Type) && (VssType_Project != m_Type)) {
		printf("invalid type %d\n", m_Type);
		return false;
	}

	// This is the version of VSS used to create the files.  This value is
	// also found in /data/version.dat, which is a 2-byte file.  This code
	// has only been tested with version 6 of VSS (since that is what I was
	// using at the time).  Any other version will be rejected, since the
	// files may not be formatted the same.  Disable this test at your own
	// risk.
	u16 version = reader.Read16();

	if (6 != version) {
		printf("unknown SourceSafe file version: %d\n", version);
		return false;
	}

	// More zero-bytes of unknown purpose.
	for (u32 i = 0; i < 4; ++i) {
		if (0 != reader.Read32()) {
			printf("non-zero value\n");
			return false;
		}
	}

	// Now we're about to read the "DH" data header chunk.  This is always
	// the first thing in an info file.  The files use RIFF-style chunks,
	// but use two-character code markers instead of four-CCs.  The next
	// two bytes are a 16-bit CRC for the chunk.  Note that according to
	// some of the fragmentary info online, the CRC may be zero, while the
	// computed CRC is non-zero.  This was never observed in the database
	// used to test this code, but ignore the CRC test when it is zero to
	// be safe.  It may be that this is only an issue for certain types of
	// chunks.
	u32 chunkSize = reader.Read32();
	u16 marker    = reader.Read16();
	u16 crc       = reader.Read16();

	if ((0 != crc) && (crc != reader.ComputeCRC(chunkSize))) {
		printf("bad CRC at start of info file\n");
		return false;
	}

	// This is always the first chunk in an info file, and is always found at
	// the same position, following the fixed-size header at the start of the
	// file.  This chunk always appears to be the same size in the database
	// that was tested.
	if (VssMarker_DataHeader != marker) {
		printf("unexpected marker %02X\n", marker);
		return false;
	}

	// This appears to be another redundant value.  It is always the same as
	// the value stored in m_Type.
	u16 type2 = reader.Read16();

	if (type2 != u16(m_Type)) {
		printf("type values do not match\n");
	}

	m_LogEntryCount = reader.Read16();
	m_HasParentFlag = reader.Read16();

	// This is a zero-terminated string, with a maximum of 32 chars, plus
	// one space for the '\0', plus one extra byte to align things to a
	// 16-bit boundary.
	//
	// Note that this string contains data that _appears_ to be meaningful,
	// but it never seems to be used.  Apparently, whoever wrote the file
	// writer code did not attempt to zero-out the unused space, so it will
	// contain whatever happened to be in the buffer -- which is usually a
	// piece of prior file data, often containing a couple of the "aaaaaaaa"
	// strings.
	//
	reader.ReadData(m_Name, 34);

	m_NameOffset   = reader.Read32();
	m_BranchNumber = reader.Read16();

//	printf("%04X %04X %04X %04X ", type2, m_LogEntryCount, m_HasParentFlag, m_BranchNumber);

	// This is the file extension of the associated data file, which will
	// always be ".A" or ".B".  This is ignored here, since this code will
	// grab whichever file it finds, regardless of extension.  VSS will
	// alternate extensions whenever it rewrites files, and this field
	// indicates which it used last.  It would be safer to pay attention
	// to this field, since some online sources indicate that VSS sometimes
	// glitches and leaves both files behind after a merge.  This was never
	// observed to be the case with the test DB, so that test was never
	// needed with this code.
	char extension[3];
	reader.ReadData(extension, 2);
	extension[2] = '\0';

	// Offset of the first and last "EL" log entry chunks in the file,
	// along with the total size of the file.  This is obviously used when
	// appending new entries to the file, since each log entry will contain
	// the offset of the previous log entry, making it easy to scan backwards
	// from the end of the file.
	m_FirstLogEntry = reader.Read32();
	m_LastLogEntry  = reader.Read32();
	m_FileSize      = reader.Read32();

	// This may be full of flags, or it may be a counter.  In the database
	// tested, this was almost always a value between 0x2000 and 0x3800 for
	// projects.  For files, this is always zero.
	u16 unknownCounter = reader.Read16();

	if (VssType_File == m_Type) {
		if (0 != unknownCounter) {
			printf("unknown counter for file is non-zero: %04X\n", unknownCounter);
		}
	}
	else {
//		printf("unknownCounter %04X\n", unknownCounter);
	}

	// The next sequence of bytes were always zero.
	u08 padding[18];
	reader.ReadData(padding, 18);

	for (int i = 0; i < 18; ++i) {
		if (0 != padding[0]) {
			printf("non-zero padding\n");
		}
	}

	return true;
}


