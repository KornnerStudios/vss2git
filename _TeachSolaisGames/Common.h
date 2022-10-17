/////////////////////////////////////////////////////////////////////////////
//
//	File: Common.h
//
//	$Header:$
//
/////////////////////////////////////////////////////////////////////////////


#ifndef _COMMON_H_
#define _COMMON_H_


typedef unsigned char	u08;
typedef signed char		s08;
typedef unsigned short	u16;
typedef signed short	s16;
typedef unsigned int	u32;
typedef signed int		s32;


#include <windows.h>
#include <stdlib.h>
#include <stdio.h>
#include <malloc.h>
#include <ctype.h>
#include <string.h>
#include <crtdbg.h>
#include <time.h>


#ifdef _DEBUG
inline void* _cdecl operator new(size_t size, const char *pFileName, int lineNum)
{
    return ::operator new(size, 1, pFileName, lineNum);
}
inline void __cdecl operator delete(void *p, const char* /*pFileName*/, int /*lineNum*/)
{
	::operator delete(p);
}
#define DEBUG_NEW new(THIS_FILE, __LINE__)
#define MALLOC_DBG(x) _malloc_dbg(x, 1, THIS_FILE, __LINE__);
#define malloc(x) MALLOC_DBG(x)
#endif // _DEBUG


#define ArraySize(x)		(sizeof(x) / (sizeof((x)[0])))
#define SafeZeroVar(x)		memset(&(x), 0, sizeof(x));
#define SafeZeroArray(x)	memset((x), 0, sizeof(x));
#define SafeRelease(x)		{ if (NULL != (x)) { (x)->Release(); (x) = NULL; } }
#define SafeDelete(x)		{ if (NULL != (x)) { delete (x);     (x) = NULL; } }
#define SafeDeleteArray(x)	{ if (NULL != (x)) { delete [] (x);  (x) = NULL; } }
#define SafeCloseHandle(x)	{ if (NULL != (x)) { CloseHandle(x); (x) = NULL; } }
#define SafeStrCopy(d, s)	StrCopy(d, s, ArraySize(d))
#define SafeStrCat(d, s)	StrCat(d, s, ArraySize(d))


#endif // _COMMON_H_

