#pragma once

#include <Windows.h>
#include <iostream>
#include <fstream>
#include <string>
#include <sstream>
using std::ifstream;
#include <filesystem>
namespace fs = std::filesystem;

void initTranslate();

std::string charToHexString(char* data, size_t dataLength);

void showText(std::string s);