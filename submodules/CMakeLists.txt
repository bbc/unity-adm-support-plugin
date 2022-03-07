set_property(GLOBAL PROPERTY USE_FOLDERS ON)
set(isTesting ${BUILD_TESTING})
set(BUILD_TESTING OFF)
set(installPrefix ${CMAKE_INSTALL_PREFIX}) # VISR will try to override this!



### VISR

set(BUILD_PYTHON_BINDINGS OFF CACHE BOOL "Python" FORCE)
set(BUILD_AUDIOINTERFACES_JACK OFF CACHE BOOL "Jack" FORCE)
set(BUILD_AUDIOINTERFACES_PORTAUDIO OFF CACHE BOOL "PortAudio" FORCE)
set(BUILD_DOCUMENTATION OFF CACHE BOOL "Docs" FORCE)
set(BUILD_MATLAB_EXTERNALS OFF CACHE BOOL "Matlab" FORCE)
set(BUILD_MAX_MSP_EXTERNALS OFF CACHE BOOL "Max" FORCE)
set(Boost_USE_STATIC_LIBS ON CACHE BOOL "Boost" FORCE)
set(BUILD_INSTALL_SHARED_LIBRARIES OFF CACHE BOOL "shared libs" FORCE)
set(BUILD_INSTALL_STATIC_LIBRARIES OFF CACHE BOOL "static libs" FORCE)
set(BUILD_INSTALL_STATIC_PIC_LIBRARIES ON CACHE BOOL "static pic libs" FORCE)
set(BUILD_USE_SNDFILE_LIBRARY OFF CACHE BOOL "libsndfile" FORCE)
set(BUILD_STANDALONE_APPLICATIONS OFF CACHE BOOL "applications" FORCE)

# @loader_path = Path of the library that loaded this shared lib. 
#                I.e, libunityadm.bundle/Content/MacOS/ 
#                 - this is where all libs will reside, including the binary for libunityadm.bundle

# set(CMAKE_INSTALL_RPATH "@loader_path") # The rpath to use in installed targets - allows libs to find each other
# set(CMAKE_MACOSX_RPATH ON) # Activate rpath support
# set(CMAKE_SKIP_BUILD_RPATH FALSE) # Build with an rpath, but...
# set(CMAKE_BUILD_WITH_INSTALL_RPATH FALSE) # ...CMAKE_INSTALL_RPATH is applied at install phase, not build phase (gets relinked)
# set(CMAKE_INSTALL_RPATH_USE_LINK_PATH FALSE) # Prevent adding other search paths. We should only be using the one we set explicitly. If that doesn't work, somethings wrong.
# set(AUTOSET_RPATH OFF CACHE BOOL "Automatically set rpath" FORCE)

# set(CMAKE_INSTALL_NAME_DIR "@loader_path") # An install name is just a pathname embedded within a dynamic library which tells the linker where that library can be found at runtime
# set(CMAKE_BUILD_WITH_INSTALL_NAME_DIR TRUE) # set the directory portion of the “install_name” field of shared libraries on Mac OSX

set(VISR_TARGET_TYPE static_pic)

add_subdirectory(rd-audio-visr EXCLUDE_FROM_ALL) # EXCLUDE_FROM_ALL prevents VISRs default install behaviour

set(VISR_TARGET_NAMES
    apputilities_${VISR_TARGET_TYPE}
    audiointerfaces_${VISR_TARGET_TYPE}
    efl_${VISR_TARGET_TYPE}
    efl_${VISR_TARGET_TYPE}_AVX
    efl_${VISR_TARGET_TYPE}_FMA
    efl_${VISR_TARGET_TYPE}_SSE
    objectmodel_${VISR_TARGET_TYPE}
    panning_${VISR_TARGET_TYPE}
    panningdsp_${VISR_TARGET_TYPE}
    pml_${VISR_TARGET_TYPE}
    rbbl_${VISR_TARGET_TYPE}
    rcl_${VISR_TARGET_TYPE}
    reverbobject_${VISR_TARGET_TYPE}
    rrl_${VISR_TARGET_TYPE}
    signalflows_${VISR_TARGET_TYPE}
    signalflowspython_${VISR_TARGET_TYPE}
    visr_${VISR_TARGET_TYPE})

foreach(VISR_TARGET ${VISR_TARGET_NAMES} )
  if(TARGET ${VISR_TARGET})
    set_target_properties(${VISR_TARGET} PROPERTIES FOLDER submodules/VISR)
    # Put library in VISR namespace so BEAR can find it
    add_library(VISR::${VISR_TARGET} ALIAS ${VISR_TARGET})
    # Clear header properties so they don't install - we don't need them, and causes issues due to relative paths on OSX
    set_target_properties(${VISR_TARGET} PROPERTIES PUBLIC_HEADER "" )
    set_target_properties(${VISR_TARGET} PROPERTIES PRIVATE_HEADER "" )
  else()
	message(WARNING "VISR target not found: " ${VISR_TARGET})
  endif()
endforeach()
set_target_properties(ffts_static kissfft_static PROPERTIES FOLDER submodules/VISR/3rd)

### BW64

add_library(IRT::bw64 INTERFACE IMPORTED GLOBAL)
set_target_properties(IRT::bw64 PROPERTIES
    INTERFACE_INCLUDE_DIRECTORIES $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/libbw64/include>)
	
### LIBADM
	
set(BUILD_SHARED_LIBS OFF)
set(UNIT_TESTS OFF CACHE BOOL "libadm unit tests" FORCE)
set(ADM_HIDE_INTERNAL_SYMBOLS OFF CACHE BOOL "hide libadm symbols by default" FORCE)
set(ADM_EXAMPLES OFF CACHE BOOL "Build ADM examples" FORCE)
add_subdirectory(libadm)
set_target_properties(adm resource_embedder PROPERTIES FOLDER submodules/libadm)

### BEAR

set(BUILD_PYTHON_BINDINGS OFF CACHE BOOL "Python" FORCE)
set(BEAR_UNIT_TESTS OFF CACHE BOOL "bear unit tests" FORCE)
set(BEAR_DOWNLOAD_DATA ON CACHE BOOL "bear data files" FORCE)
set(BEAR_VISR_LIB_TYPE "${VISR_TARGET_TYPE}" CACHE STRING "bear visr lib type" FORCE)
add_subdirectory(bear/visr_bear EXCLUDE_FROM_ALL)
set_target_properties(bear PROPERTIES FOLDER submodules/bear)
set_target_properties(ear libear_resource_embedder PROPERTIES FOLDER submodules/libear)
add_dependencies(bear ${VISR_TARGET_NAMES})



### LIBSAMPLERATE

set(LIBSAMPLERATE_EXAMPLES OFF CACHE BOOL "LIBSAMPLERATE_EXAMPLES" FORCE)
set(LIBSAMPLERATE_INSTALL OFF CACHE BOOL "LIBSAMPLERATE_INSTALL" FORCE)
set(LIBSAMPLERATE_INSTALL_PKGCONFIG_MODULE OFF CACHE BOOL "LIBSAMPLERATE_INSTALL_PKGCONFIG_MODULE" FORCE)
add_subdirectory(libsamplerate)
set_target_properties(samplerate PROPERTIES FOLDER submodules/libsamplerate)




set(BUILD_TESTING ${isTesting})
set(CMAKE_INSTALL_PREFIX ${installPrefix} CACHE PATH " " FORCE)
set(CPACK_PACKAGE_INSTALL_DIRECTORY ${CMAKE_INSTALL_PREFIX} CACHE PATH " " FORCE)