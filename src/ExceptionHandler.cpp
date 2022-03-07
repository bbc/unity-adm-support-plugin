#include "ExceptionHandler.h"

namespace {
    ExceptionHandler* exceptionHandler = nullptr;
}

ExceptionHandler* getExceptionHandler()
{
    // TODO - need a destroy method to tidy this up when unloading lib
    if(!exceptionHandler) {
        exceptionHandler = new ExceptionHandler();
    }
    return exceptionHandler;
}

ExceptionHandler::ExceptionHandler()
{
}

ExceptionHandler::~ExceptionHandler()
{
}

const char * ExceptionHandler::getLatestException()
{
    return latestExceptionMsg.c_str();
}

void ExceptionHandler::logException(std::string ex)
{
    latestExceptionMsg = ex;
}

void ExceptionHandler::clearException()
{
    latestExceptionMsg = "";
}
