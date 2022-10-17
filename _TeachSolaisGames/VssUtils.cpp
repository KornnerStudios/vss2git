/////////////////////////////////////////////////////////////////////////////
//
//	File: VssUtils.cpp
//
//	$Header:$
//
/////////////////////////////////////////////////////////////////////////////


#include "Common.h"
#include "VssUtils.h"


#ifdef _DEBUG
#define new DEBUG_NEW
#undef THIS_FILE
static char THIS_FILE[] = __FILE__;
#endif


/////////////////////////////////////////////////////////////////////////////
//
//	IsVssInfoFileName()
//
//	Checks if the file name is in the format "aaaaaaaa", with exactly 8
//	characters, using only 'a' through 'z'.  Both upper case and lower case
//	are permitted, since cases are used in different places.
//
bool IsVssInfoFileName(wchar_t filename[])
{
	for (u32 i = 0; i < 8; ++i) {
		if (0 == iswalpha(filename[i])) {
			return false;
		}
	}

	return ('\0' == filename[8]);
}


/////////////////////////////////////////////////////////////////////////////
//
//	IsVssDataFileName()
//
//	Checks if the name is formatted as "aaaaaaaa.a".  The file extension can
//	be either 'a' or 'b'.  Since it may be possible for 'c' to occur, any
//	letter 'a' - 'z' is allowed for the file extension.
//
//	Note that for the database used in testing, only 'a' and 'b' were ever
//	used as the file extension.
//
bool IsVssDataFileName(wchar_t filename[])
{
	for (u32 i = 0; i < 8; ++i) {
		if (0 == iswalpha(filename[i])) {
			return false;
		}
	}

	return('.' == filename[8]) && iswalpha(filename[9]) && ('\0' == filename[10]);
}


/////////////////////////////////////////////////////////////////////////////
//
//	VssNameToNumber()
//
//	Takes an 8-char string, formatted as 'aaaaaaaa' through 'zzzzzzzz'.
//	Returns the integer value of the string.
//
//	Note that this code ignores the possibility of overflow, since 'zzzzzzzz'
//	maps to a 38-bit value.  If you were to ever encounter a VSS database
//	with this many files, you'd need to change this code to return a 64-bit
//	value to avoid overflow.
//
int VssNameToNumber(wchar_t name[])
{
	int num = 0;

	// The number is encoded in base-26, using the letters 'a' through 'z'.
	// Need to scan from left to right, which is the reverse of the
	// conventional symbol ordering used in computing.
	//
	for (int i = 7; i >= 0; --i) {
		if (('a' <= name[i]) && (name[i] <= 'z')) {
			num = (num * 26) + u32(name[i] - 'a');
		}
		else if (('A' <= name[i]) && (name[i] <= 'Z')) {
			num = (num * 26) + u32(name[i] - 'A');
		}
		else {
			return -1;
		}
	}

	return num;
}


/////////////////////////////////////////////////////////////////////////////
//
//	VssNameToNumber()
//
//	Takes an 8-char string, formatted as 'aaaaaaaa' through 'zzzzzzzz'.
//	Returns the integer value of the string.
//
//	Returns -1 if this is not a valid name.  It is common to find a name
//	field in the code that is stored as an empty string.  For example, each
//	project stores the name of the project that contains it -- the root
//	project has not parent, so this field is an empty string in that case.
//
//	Note that this code ignores the possibility of overflow, since 'zzzzzzzz'
//	maps to a 38-bit value.  If you were to ever encounter a VSS database
//	with this many files, you'd need to change this code to return a 64-bit
//	value to avoid overflow.
//
int VssNameToNumber(char name[])
{
	int num = 0;

	// The number is encoded in base-26, using the letters 'a' through 'z'.
	// Need to scan from left to right, which is the reverse of the
	// conventional symbol ordering used in computing.
	//
	for (int i = 7; i >= 0; --i) {
		if (('a' <= name[i]) && (name[i] <= 'z')) {
			num = (num * 26) + u32(name[i] - 'a');
		}
		else if (('A' <= name[i]) && (name[i] <= 'Z')) {
			num = (num * 26) + u32(name[i] - 'A');
		}
		else {
			return -1;
		}
	}

	return num;
}


/////////////////////////////////////////////////////////////////////////////
//
//	VssNumberToName()
//
void VssNumberToName(u32 number, wchar_t name[])
{
	for (int i = 0; i < 8; ++i) {
		name[i] = 'a' + wchar_t(number % 26);
		number /= 26;
	}
	name[8] = '\0';
}


/////////////////////////////////////////////////////////////////////////////
//
//	VssNumberToName()
//
void VssNumberToName(u32 number, char name[])
{
	for (int i = 0; i < 8; ++i) {
		name[i] = 'a' + char(number % 26);
		number /= 26;
	}
	name[8] = '\0';
}


