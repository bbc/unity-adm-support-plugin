cmake_minimum_required(VERSION 3.10.0)

project(libunityadm VERSION 1.0.0 LANGUAGES CXX)

set(Boost_USE_STATIC_LIBS ON)
add_subdirectory(submodules)
add_subdirectory(src)

message(${CMAKE_SYSTEM_NAME})
