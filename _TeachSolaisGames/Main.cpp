

#include "Common.h"
#include "VssTree.h"
#include "VssUtils.h"


#ifdef _DEBUG
#define new DEBUG_NEW
#undef THIS_FILE
static char THIS_FILE[] = __FILE__;
#endif


void ImportDatabase(wchar_t path[])
{
	VssTree tree;
	if (false == tree.Import(path)) {
		printf("error: could not import database\n");
		return;
	}
}


int main(int argc, char *argv[])
{
	DWORD t0 = GetTickCount();
	ImportDatabase(L"TestDB");
//	ImportDatabase(L"D:\\SourceDB\\");
	DWORD t1 = GetTickCount();

	printf("processed in %1.3f seconds\n", float(t1 - t0) / 1000.0f);

	_CrtDumpMemoryLeaks();

	return 0;
}


