/////////////////////////////////////////////////////////////////////////////
//
//	File: VssTypes.h
//
//	$Header:$
//
/////////////////////////////////////////////////////////////////////////////


#ifndef _VSS_TYPES_H_
#define _VSS_TYPES_H_


#define VssType_File		2
#define VssType_Project		1

#define VssMarker_BranchFile	0x4642		// "BF"
#define VssMarker_CheckOut		0x4643		// "CF"
#define VssMarker_Child			0x504A		// "JP"
#define VssMarker_Comment		0x434D		// "MC"
#define VssMarker_DataHeader	0x4844		// "DH"
#define VssMarker_Difference	0x4446		// "FD", sequence of changes against current file
#define VssMarker_LogEntry		0x4C45		// "EL", record of changes
#define VssMarker_NameHeader	0x4E48		// "HN" only found at start of names.dat
#define VssMarker_ParentFolder	0x4650		// "PF"
#define VssMarker_ShortName		0x4E53		// "SN" only found in names.dat

#define VssOpcode_Labeled				 0
#define VssOpcode_CreatedProject		 1
#define VssOpcode_AddedProject			 2
#define VssOpcode_AddedFile				 3
#define VssOpcode_DestroyedProject		 4
#define VssOpcode_DestroyedFile			 5
#define VssOpcode_DeletedProject		 6
#define VssOpcode_DeletedFile			 7
#define VssOpcode_RecoveredProject		 8
#define VssOpcode_RecoveredFile			 9
#define VssOpcode_RenamedProject		10
#define VssOpcode_RenamedFile			11
#define VssOpcode_MovedProjectFrom		12
#define VssOpcode_MovedProjectTo		13
#define VssOpcode_SharedFile			14
#define VssOpcode_BranchedFile			15
#define VssOpcode_CreatedFile			16
#define VssOpcode_CheckedInFile			17
#define VssOpcode_CheckedInProject		18
#define VssOpcode_RolledBack			19
#define VssOpcode_ArchivedVersionFile	20
#define VssOpcode_RestoredVersionFile	21
#define VssOpcode_ArchivedFile			22
#define VssOpcode_ArchivedProject		23
#define VssOpcode_RestoredFile			24
#define VssOpcode_RestoredProject		25
#define VssOpcode_PinnedFile			26
#define VssOpcode_UnpinnedFile			27


struct VssNode_t
{
	u32  InfoSize;
	u08* pInfo;
	u32  DataSize;
	u08* pData;
	u32  Type;
	u32  ParentID;
};


#endif // _VSS_TYPES_H_


