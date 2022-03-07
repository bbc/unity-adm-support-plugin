#pragma once
#include <string>

class ExceptionHandler
{
public:
    ExceptionHandler();
    ~ExceptionHandler();

    const char* getLatestException();
    void logException(std::string ex);
    void clearException();

private:
    std::string latestExceptionMsg{""};
};

ExceptionHandler* getExceptionHandler();
