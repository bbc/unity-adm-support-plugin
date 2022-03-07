set_property(GLOBAL PROPERTY USE_FOLDERS ON)
set(isTesting ${BUILD_TESTING})
set(BUILD_TESTING OFF)

### VISR

set(BUILD_PYTHON_BINDINGS OFF CACHE BOOL "Python" FORCE)
set(BUILD_AUDIOINTERFACES_JACK OFF CACHE BOOL "Jack" FORCE)
set(BUILD_AUDIOINTERFACES_PORTAUDIO OFF CACHE BOOL "PortAudio" FORCE)
set(BUILD_DOCUMENTATION OFF CACHE BOOL "Docs" FORCE)
set(BUILD_MATLAB_EXTERNALS OFF CACHE BOOL "Matlab" FORCE)
set(BUILD_MAX_MSP_EXTERNALS OFF CACHE BOOL "Max" FORCE)
set(Boost_USE_STATIC_LIBS OFF CACHE BOOL "Boost" FORCE)
set(BUILD_USE_SNDFILE_LIBRARY OFF CACHE BOOL "libsndfile" FORCE)
set(BUILD_STANDALONE_APPLICATIONS OFF CACHE BOOL "applications" FORCE)

# @loader_path = Path of the library that loaded this shared lib. 
#                I.e, libunityadm.bundle/Content/MacOS/ 
#                 - this is where all libs will reside, including the binary for libunityadm.bundle

set(CMAKE_INSTALL_RPATH "@loader_path") # The rpath to use in installed targets - allows libs to find each other
set(CMAKE_MACOSX_RPATH ON) # Activate rpath support
set(CMAKE_SKIP_BUILD_RPATH FALSE) # Build with an rpath, but...
set(CMAKE_BUILD_WITH_INSTALL_RPATH FALSE) # ...CMAKE_INSTALL_RPATH is applied at install phase, not build phase (gets relinked)
set(CMAKE_INSTALL_RPATH_USE_LINK_PATH FALSE) # Prevent adding other search paths. We should only be using the one we set explicitly. If that doesn't work, somethings wrong.
set(AUTOSET_RPATH OFF CACHE BOOL "Automatically set rpath" FORCE)

set(CMAKE_INSTALL_NAME_DIR "@loader_path") # An install name is just a pathname embedded within a dynamic library which tells the linker where that library can be found at runtime
set(CMAKE_BUILD_WITH_INSTALL_NAME_DIR TRUE) # set the directory portion of the “install_name” field of shared libraries on Mac OSX

add_subdirectory(rd-audio-visr EXCLUDE_FROM_ALL) # EXCLUDE_FROM_ALL prevents VISRs default install behaviour
	
set(VISR_SHARED_TARGETS
    apputilities_shared
    audiointerfaces_shared
    efl_shared
    objectmodel_shared
    panning_shared
    panningdsp_shared
    pml_shared
    rbbl_shared
    rcl_shared
    reverbobject_shared
    rrl_shared
    signalflows_shared
    signalflowspython_shared
    visr_shared)

set_target_properties(${VISR_SHARED_TARGETS} PROPERTIES FOLDER VISR)

foreach(SHARED_TARGET ${VISR_SHARED_TARGETS} )
  # Put library in VISR namespace so BEAR can find it
  add_library(VISR::${SHARED_TARGET} ALIAS ${SHARED_TARGET})
  # Clear header properties so they don't install - we don't need them, and causes issues due to relative paths on OSX
  set_target_properties(${SHARED_TARGET} PROPERTIES PUBLIC_HEADER "" )
  set_target_properties(${SHARED_TARGET} PROPERTIES PRIVATE_HEADER "" )
endforeach()

if(WIN32)
  install(TARGETS ${VISR_SHARED_TARGETS} 
    RUNTIME DESTINATION ${PROJECT_SOURCE_DIR}/UnityAdmProject/Assets/UnityADM/Plugins)
endif()

if(APPLE)
  install(TARGETS ${VISR_SHARED_TARGETS}
    # $<TARGET_BUNDLE_CONTENT_DIR:libunityadm> = ${PROJECT_SOURCE_DIR}/UnityAdmProject/Assets/UnityADM/Plugins/libunityadm.bundle/Content
    LIBRARY DESTINATION $<TARGET_BUNDLE_CONTENT_DIR:libunityadm>/MacOS) # Installs all VISR libs to libunityadm.bundle/Content/MacOS/
endif()

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
set_target_properties(adm resource_embedder PROPERTIES FOLDER libadm)

### BEAR

set(BUILD_PYTHON_BINDINGS OFF CACHE BOOL "Python" FORCE)
set(BEAR_UNIT_TESTS OFF CACHE BOOL "bear unit tests" FORCE)
set(BEAR_DOWNLOAD_DATA ON CACHE BOOL "bear data files" FORCE)
add_subdirectory(bear/visr_bear)
set_target_properties(bear libear_resource_embedder PROPERTIES FOLDER bear)
set_target_properties(ear PROPERTIES FOLDER libear)
add_dependencies(bear ${VISR_SHARED_TARGETS})

### LIBSAMPLERATE

set(LIBSAMPLERATE_EXAMPLES OFF CACHE BOOL "LIBSAMPLERATE_EXAMPLES" FORCE)
set(LIBSAMPLERATE_INSTALL OFF CACHE BOOL "LIBSAMPLERATE_INSTALL" FORCE)
set(LIBSAMPLERATE_INSTALL_PKGCONFIG_MODULE OFF CACHE BOOL "LIBSAMPLERATE_INSTALL_PKGCONFIG_MODULE" FORCE)
add_subdirectory(libsamplerate)
set_target_properties(samplerate PROPERTIES FOLDER 3rd)

set(BUILD_TESTING ${isTesting})