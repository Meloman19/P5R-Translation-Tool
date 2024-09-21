#include "hook.h"

void initTranslate()
{
	TCHAR szFileName[MAX_PATH];
	GetModuleFileName(NULL, szFileName, MAX_PATH);

	if (fs::path(szFileName).filename() != fs::path("P5R.exe"))
	{
		return;
	}

	ifstream transl;
	transl.open("TRANSL.DAT", std::ifstream::in | std::ifstream::binary);

	if (!transl)
	{
		MessageBox(NULL, TEXT("Not found TRANSL.DAT"), TEXT("Loader"), 0);
		return;
	}

	BOOL first = true;
	int counter = 0;
	while (true)
	{
		int64_t baseAddress;
		if (!transl.read(reinterpret_cast<char*>(&baseAddress), sizeof(baseAddress)))
		{
			break;
		}
		int32_t dataSize;
		if (!transl.read(reinterpret_cast<char*>(&dataSize), sizeof(dataSize)))
		{
			break;
		}

		char* data = new char[dataSize];
		if (!transl.read(data, dataSize))
		{
			delete[] data;
			MessageBox(NULL, TEXT("Error read char array"), TEXT("Loader"), 0);
			return;
		}

		auto baseAddressPtr = reinterpret_cast<LPVOID>(baseAddress);
		DWORD OldProtection;
		VirtualProtect(baseAddressPtr, dataSize, PAGE_READWRITE, &OldProtection);
		CopyMemory(baseAddressPtr, data, dataSize);
		VirtualProtect(baseAddressPtr, dataSize, OldProtection, &OldProtection);

		delete[] data;
	}

	transl.close();
	return;
}

std::string charToHexString(char* data, size_t dataLength)
{
	std::stringstream ss;
	for (int i = 0; i < dataLength; ++i)
	{
		ss << std::hex << (int)data[i];
		ss << " ";
	}
	return ss.str();
}

void showText(std::string s)
{
	std::wstring stemp = std::wstring(s.begin(), s.end());
	LPCWSTR sw = stemp.c_str();

	MessageBox(NULL, sw, TEXT("Loader"), 0);
}