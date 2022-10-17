/////////////////////////////////////////////////////////////////////////////
//
//	File: VssScanChild.cpp
//
//	$Header:$
//
//
//	Strictly speaking, these chunks are "JP" or "project" chunks, but the
//	word "project" is used poorly in VSS, so this data is called "child"
//	data, since it is a reference to a child -- either a file or a folder --
//	within the parent directory (project).
//
/////////////////////////////////////////////////////////////////////////////


#include "Common.h"
#include "VssScanChild.h"


#ifdef _DEBUG
#define new DEBUG_NEW
#undef THIS_FILE
static char THIS_FILE[] = __FILE__;
#endif


/////////////////////////////////////////////////////////////////////////////
//
//	constructor
//
VssScanChild::VssScanChild(void)
{
}


/////////////////////////////////////////////////////////////////////////////
//
//	Scan()
//
bool VssScanChild::Scan(BinaryReader &reader)
{
	m_Type       = reader.Read16();
	m_Flags      = reader.Read16();
	m_NameFlags  = reader.Read16();
	reader.ReadData(m_Name, 34);
	m_NameOffset = reader.Read32();
	u16 zero     = reader.Read16();
	reader.ReadData(m_DBName, 10);

	if (0 != zero) {
		printf("child uses the zero bits: 0x%04X\n", zero);
	}

	return true;
}


/////////////////////////////////////////////////////////////////////////////
//
//	Dump()
//
void VssScanChild::Dump(void)
{
/*
	if (0x0001 & m_Flags) {
		printf("child has been deleted\n");
	}
	if (0x0002 & m_Flags) {
		printf("child contains binary data\n");
	}
	// This flag does not appear to be used.
	if (0x0004 & m_Flags) {
		printf("child flag 0x0004 was found\n");
	}
	if (0x0008 & m_Flags) {
		printf("child is shared\n");
	}
*/

	printf("0x%04X 0x%04X 0x%08X 0x%04X\n", m_Flags, m_NameFlags, m_NameOffset, m_Zero);
}

