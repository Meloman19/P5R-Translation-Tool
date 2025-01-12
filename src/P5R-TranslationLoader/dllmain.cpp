#include <Windows.h>
#include "UltimateProxyDLL.h"
#include "hook.h"

BOOL WINAPI DllMain(HINSTANCE hinstDLL, DWORD fdwReason, LPVOID lpvReserved)
{
	if (fdwReason == DLL_PROCESS_ATTACH)
	{
		try
		{
			UPD::MuteLogging();
			//UPD::OpenDebugTerminal();
			UPD::CreateProxy(hinstDLL);
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