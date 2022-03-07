cmake_minimum_required(VERSION 3.13.0)

project(libunityadm LANGUAGES CXX)

if(CMAKE_INSTALL_PREFIX_INITIALIZED_TO_DEFAULT)
	set(CMAKE_INSTALL_PREFIX ${PROJECT_SOURCE_DIR}/UnityAdmProject CACHE PATH " " FORCE)
	set(CMAKE_INSTALL_PREFIX_INITIALIZED_TO_DEFAULT false CACHE BOOL " " FORCE)
endif()
set(CPACK_PACKAGE_INSTALL_DIRECTORY ${CMAKE_INSTALL_PREFIX} CACHE PATH " " FORCE)

set(UNITY_EXECUTABLE "" CACHE FILEPATH "Unity executable used for package compilation")

set(CPACK_GENERATOR "External"  CACHE STRING " " FORCE)
set(CPACK_SOURCE_GENERATOR "External" CACHE STRING " " FORCE)

configure_file(${PROJECT_SOURCE_DIR}/package.cmake.in "${PROJECT_SOURCE_DIR}/package.cmake" @ONLY)
set(CPACK_EXTERNAL_PACKAGE_SCRIPT "${PROJECT_SOURCE_DIR}/package.cmake")
include(CPack)

set(Boost_USE_STATIC_LIBS ON)

set(_CMAKE_MODULE_PATH ${CMAKE_MODULE_PATH})
list(PREPEND CMAKE_MODULE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/dummy_cmake_modules")
add_subdirectory(submodules)
set(CMAKE_MODULE_PATH ${_CMAKE_MODULE_PATH})

add_subdirectory(src)
set_property(DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR} PROPERTY VS_STARTUP_PROJECT libunityadm)

set(copyrightFileSrc "COPYRIGHT")
set(licenseFileSrc "LICENSE")
set(copyrightFileDstDir "Assets/UnityAdm")
set(licenseFileDstDir "Assets/UnityAdm")

install(FILES ${copyrightFileSrc} DESTINATION ${copyrightFileDstDir})
install(FILES ${licenseFileSrc} DESTINATION ${licenseFileDstDir})
