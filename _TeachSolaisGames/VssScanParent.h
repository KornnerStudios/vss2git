/////////////////////////////////////////////////////////////////////////////
//
//	File: VssScanParent.h
//
//	$Header:$
//
/////////////////////////////////////////////////////////////////////////////


#ifndef _VSS_SCAN_PARENT_H_
#define _VSS_SCAN_PARENT_H_


#include "BinaryReader.h"
#include "VssTypes.h"


class VssScanParent
{
public:
	u32 m_PreviousOffset;
	int m_ParentIndex;

	void Scan(BinaryReader &reader);
	void Dump(void);
};


#endif // _VSS_SCAN_PARENT_H_


