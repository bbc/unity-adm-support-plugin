set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

set(SOURCE_FILES
  main.h
  main.cpp
  Readers.h
  Readers.cpp
  Audio.h
  Audio.cpp
  Metadata.h
  Metadata.cpp
  BearRender.h
  BearRender.cpp
  Helpers.h
  ExceptionHandler.h
  ExceptionHandler.cpp
)

add_library(libunityadm MODULE
  ${SOURCE_FILES}
)

if(APPLE)
  set_target_properties(libunityadm PROPERTIES
    MACOSX_BUNDLE TRUE
    BUNDLE TRUE
  )
  # delete rpath before add, or add will fail. 
  # `|| (exit 0)` ensures delete fails silently if path not present (that's OK, and normal for a first run)
  # We could just allow add to fail silently if already present, but it might fail for another reason, and we don't want to silence that. 
  #  ...hence deleting first means add should always succeed, and any failure needs investigation.
  # @executable_path = path of the binary - i.e, libunityadm.bundle/Content/MacOS
  # All visr deps are installed to this directory too, so setting the rpath there allows libunityadm to find them
  add_custom_command(TARGET libunityadm
    POST_BUILD
    COMMAND ${CMAKE_INSTALL_NAME_TOOL} -delete_rpath "@executable_path" $<TARGET_FILE:libunityadm> >/dev/null 2>/dev/null || (exit 0)
    COMMAND ${CMAKE_INSTALL_NAME_TOOL} -add_rpath "@executable_path" $<TARGET_FILE:libunityadm>
  )
endif()

target_include_directories(libunityadm
    PUBLIC
        $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}>
        #$<TARGET_PROPERTY:IRT::bw64,INTERFACE_INCLUDE_DIRECTORIES> - not required - pulled in during target_link_libraries
        #$<TARGET_PROPERTY:adm,INSTALL_INCLUDE_DIR> # Gets to submodules\libadm\include - not required - pulled in during target_link_libraries
        #$<TARGET_PROPERTY:bear,SOURCE_DIR> # - not required - pulled in during target_link_libraries
        #$<TARGET_PROPERTY:bear,INCLUDE_DIR> # - not required - pulled in during target_link_libraries
)

target_link_libraries(libunityadm
    PUBLIC
      IRT::bw64
      adm
	  bear
      samplerate
)

target_compile_features(libunityadm
    PRIVATE
        cxx_std_17
)

install(TARGETS libunityadm
    DESTINATION Assets/UnityAdm/Plugins
)

set_property(TARGET libunityadm PROPERTY
  VS_DEBUGGER_COMMAND ${UNITY_EXECUTABLE}
)

set_property(TARGET libunityadm PROPERTY
  VS_DEBUGGER_COMMAND_ARGUMENTS "-projectPath ${PROJECT_SOURCE_DIR}/UnityAdmProject"
)

ExternalProject_Get_Property(default_tensorfile DOWNLOADED_FILE)

add_dependencies(libunityadm default_tensorfile)
install(FILES ${DOWNLOADED_FILE} DESTINATION Assets/UnityAdm/Data)
