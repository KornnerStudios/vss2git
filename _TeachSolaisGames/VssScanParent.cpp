/////////////////////////////////////////////////////////////////////////////
//
//	File: VssScanParent.cpp
//
//	$Header:$
//
/////////////////////////////////////////////////////////////////////////////


#include "Common.h"
#include "VssScanParent.h"
#include "VssUtils.h"


#ifdef _DEBUG
#define new DEBUG_NEW
#undef THIS_FILE
static char THIS_FILE[] = __FILE__;
#endif


/////////////////////////////////////////////////////////////////////////////
//
//	Scan()
//
void VssScanParent::Scan(BinaryReader &reader)
{
	// This is the offset of the previous parent chunk.  This will be zero
	// for the first parent chunk.  Additional parent chunks only appear if
	// the file has been shared.
	m_PreviousOffset = reader.Read32();

	// This is the name of the parent folder.  If that shared link to a file
	// is branched, this parent chunk will have the parent name zeroed out.
	// Since this code converts database file names back to the numerical
	// index, m_ParentIndex will be set to -1 if this file was branched off.
	char parent[10];
	reader.ReadData(parent, 10);

	m_ParentIndex = VssNameToNumber(parent);
}


/////////////////////////////////////////////////////////////////////////////
//
//	Dump()
//
void VssScanParent::Dump(void)
{
	printf("parent: %d at 0x%08X\n", m_ParentIndex, m_PreviousOffset);
}

