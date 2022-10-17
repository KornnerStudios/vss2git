/////////////////////////////////////////////////////////////////////////////
//
//	File: VssScanChild.h
//
//	$Header:$
//
/////////////////////////////////////////////////////////////////////////////


#ifndef _VSS_SCAN_CHILD_H_
#define _VSS_SCAN_CHILD_H_


#include "BinaryReader.h"
#include "VssTypes.h"


#define VssChildFlag_Deleted		0x0001
#define VssChildFlag_BinaryData		0x0002
#define VssChildFlag_Shared			0x0008


class VssScanChild
{
public:
	// This will be set to one of two values:
	//    0x0001 = VssType_Project
	//    0x0002 = VssType_File
	//
	u16	m_Type;

	// This is a bitmask, indicating properties about the link.
	//    0x0001 = VssChildFlag_Deleted
	//    0x0002 = VssChildFlag_BinaryData
	//    0x0004 = ????
	//    0x0008 = VssChildFlag_Shared
	//
	u16 m_Flags;

	// This flag is redundant with m_Type.
	//    0x0000 = file
	//    0x0001 = directory
	//
	u16 m_NameFlags;

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

	// Always zero.  More padding?  Or is this something useful?
	//
	u16 m_Zero;

	// Name of the database file name, in "aaaaaaaa" format.
	//
	char m_DBName[10];

	VssScanChild(void);

	bool Scan(BinaryReader &reader);
	void Dump(void);
};


#endif // _VSS_SCAN_PROJECT_H_


