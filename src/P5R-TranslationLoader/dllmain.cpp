#include <Windows.h>
#include "UniversalProxyDLL.h"
#include "hook.h"

BOOL APIENTRY DllMain(HMODULE hModule, DWORD  ul_reason_for_call, LPVOID lpReserved
)
{
	if (ul_reason_for_call == DLL_PROCESS_ATTACH)
	{
		DisableThreadLibraryCalls(hModule);
		try
		{
			UPD::MuteLogging();
			//UPD::OpenDebugTerminal();
			UPD::CreateProxy(hModule);
			initTranslate();
		}
		catch (std::runtime_error e)
		{
			std::cout << e.what() << std::endl;
			return FALSE;
		}
	}

	return TRUE;
}