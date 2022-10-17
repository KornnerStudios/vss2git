/////////////////////////////////////////////////////////////////////////////
//
//	File: VssScanHeader.h
//
//	$Header:$
//
/////////////////////////////////////////////////////////////////////////////


#ifndef _VSS_SCAN_HEADER_H_
#define _VSS_SCAN_HEADER_H_


#include "VssTypes.h"
#include "BinaryReader.h"


class VssScanHeader
{
public:
	// This will be set to one of two values:
	//    0x0002 = VssType_File
	//    0x0001 = VssType_Project
	//
	u32 m_Type;

	// Stores the number of log entry ("EL") chunks stored in the file.
	//
	u16 m_LogEntryCount;

	// For all files, this is zero.
	// For all folders, this is 0x0001.
	// The exception is that the for the root folder, this is zero.
	// Possibly this is used to indicate whether you can trace up one level
	// when looking at a folder.
	//
	u16 m_HasParentFlag;

	// The name of the file.  This looks like it can be a 32-char
	// string, with extra space for the '\0', and another one to keep
	// the fields aligned to 16-bit boundaries.
	//
	char m_Name[34];

	// This is an offset into the names.dat file, which contains the 8.3
	// names for files.  It will indicate the beginning of a "SN" name
	// mapping chunk.  You can extract that data from names.dat if you
	// really need to map to the exact 8.3 short file name.
	// 
	// (e.g., this would map "reallylongname.txt" to something like
	// "really~1.txt")
	//
	// This will only be non-zero for files with names that do not fit into
	// the 8.3 format.  For file names that conform to the 8.3 format, this
	// field will be zero.
	//
	u32 m_NameOffset;

	// If the file has been branched, this field will contain the version
	// number at which it was branched.
	//
	// If the branch number is 1, the file has never been branched.
	//
	u16 m_BranchNumber;

	// Offset of the first log entry ("EL") chunk in the file.
	//
	u32 m_FirstLogEntry;

	// Offset of the last log entry ("EL") chunk in the file.
	//
	// Note that each chunk contains the offset of the one before it,
	// allowing VSS to locate the last log entry chunk in the file, then
	// scan backwards through the file.
	//
	u32 m_LastLogEntry;

	// The size of the file.  VSS probably uses this to know where to write
	// data when appending new log entry chunks to the end of the file.
	//
	u32 m_FileSize;

	bool Scan(BinaryReader &reader);
};


#endif // _VSS_SCANNER_H_


