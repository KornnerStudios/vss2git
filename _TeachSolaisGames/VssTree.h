/////////////////////////////////////////////////////////////////////////////
//
//	File: VssTree.h
//
//	$Header:$
//
/////////////////////////////////////////////////////////////////////////////


#ifndef _VSS_TREE_H_
#define _VSS_TREE_H_


#include "VssTypes.h"


class VssTree
{
private:
	u32			m_NodeCount;
	u32			m_MaxNodes;
	VssNode_t*	m_pNodes;
	u32			m_InfoCount;
	u32			m_DataCount;
	u32			m_MemorySize;

public:
	VssTree(void);
	~VssTree(void);

	void Free(void);
	bool Import(wchar_t path[]);
	bool ImportDir(wchar_t dirname[]);
	bool ImportFile(wchar_t filename[], u32 index, bool isInfo);
	bool AssembleDirectoryLinks(u32 index, u32 depth, char path[]);
	bool LookForUnused(void);
};


#endif // _VSS_TREE_H_


