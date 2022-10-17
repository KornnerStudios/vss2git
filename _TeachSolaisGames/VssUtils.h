/////////////////////////////////////////////////////////////////////////////
//
//	File: VssUtils.h
//
//	$Header:$
//
/////////////////////////////////////////////////////////////////////////////


#ifndef _VSS_UTILS_H_
#define _VSS_UTILS_H_


bool IsVssInfoFileName(wchar_t filename[]);
bool IsVssDataFileName(wchar_t filename[]);
int  VssNameToNumber(wchar_t name[]);
int  VssNameToNumber(char name[]);
void VssNumberToName(u32 number, wchar_t name[]);
void VssNumberToName(u32 number, char name[]);


#endif // _VSS_UTILS_H_

