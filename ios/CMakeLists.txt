cmake_minimum_required(VERSION 3.15.0 FATAL_ERROR)

set(CMAKE_SYSTEM_VERSION 10.0 CACHE STRING "" FORCE)
include(${CMAKE_SOURCE_DIR}/../vcpkg/scripts/buildsystems/vcpkg.cmake)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_CXX_FLAGS "-x objective-c++")

set(CMAKE_CONFIGURATION_TYPES "Debug;Release") 

set(IOSAPPNAME ${PROJECT_NAME})
set(CMAKE_MODULE_PATH ${CMAKE_SOURCE_DIR})

set (BGFX_INCLUDE_ROOT ${CMAKE_INSTALL_PREFIX}/include)
set (BGFX_INCLUDE ${BGFX_INCLUDE_ROOT}/bgfx)
set (BGFX_LIB ${CMAKE_INSTALL_PREFIX}/lib)

################################################################################
# Source groups
################################################################################
set(Header_Files
    "entry_p.h"
    "entry.h"
    "cmd.h"
    "input.h"
)  

source_group("Header Files" FILES ${Header_Files})

set(Source_Files   
    "main.mm"
    "entry.cpp"
    "cmd.cpp"
    "input.cpp"
    "bgfx_utils.cpp"   
    )


source_group("Source Files" FILES ${Source_Files})

set (IOSPROPS 
    ${ALL_SHADERS}
    ${OUT_RESOURCES})

set_source_files_properties(${IOSPROPS} PROPERTIES GENERATED true)
set(ALL_FILES    
    ${Header_Files} 
    ${Source_Files}
    ${IOSPROPS}
)

################################################################################
# Target
################################################################################

add_executable(${IOSAPPNAME} MACOSX_BUNDLE ${ALL_FILES})

find_library(FOUNDATIONLIB Foundation)
find_library(COREFOUNDATIONLIB CoreFoundation)
find_library(METALLIB Metal)
find_library(QUARTZCORELIB QuartzCore)
find_library(METALKITLIB MetalKit)
find_library(UIKITLIB UIKit)
find_library(MODELIOLIB ModelIO)
find_library(OPENGL OpenGLES)
find_library(AVFAUDIO AVFAudio)
find_library(AUDIOTOOLBOX AudioToolbox)
find_package(libzip CONFIG REQUIRED)
find_package(fmt CONFIG REQUIRED)
find_package(libzip CONFIG REQUIRED)
find_package(CURL CONFIG REQUIRED)

set (PLATFORMLIBS 
"${FOUNDATIONLIB};"
"${COREFOUNDATIONLIB};"
"${METALLIB};"
"${QUARTZCORELIB};"
"${METALKITLIB};"
"${UIKITLIB};"
"${MODELIOLIB};"
"${OPENGL};"
"${AVFAUDIO};"
"${AUDIOTOOLBOX};"
)

set_target_properties(${IOSAPPNAME} PROPERTIES MACOSX_BUNDLE_GUI_IDENTIFIER ${BUNDLE_ID})

foreach (SHDR ${IOSPROPS})

message(SHDR ${SHDR})
set_source_files_properties(${SHDR} PROPERTIES MACOSX_PACKAGE_LOCATION Resources)

endforeach()

target_include_directories(${IOSAPPNAME} PUBLIC
    "${CMAKE_CURRENT_SOURCE_DIR}/"
    "${CMAKE_SOURCE_DIR}/core/"
    "${CMAKE_BINARY_DIR}/"
    "${BGFX_INCLUDE_ROOT}"
    "${BX_INC_DIR_COMPAT}"
    "${BGFX_INC_DIR}"
    )

target_link_libraries(${IOSAPPNAME} LINK_PUBLIC
    ${PLATFORMLIBS}
    zlibstatic
    libzip::zip
    core
    fmt::fmt-header-only
    leveldb
    CURL::libcurl
    ${VCPKG_INSTALL_PATH}/lib/${CMAKE_STATIC_LIBRARY_PREFIX}Bullet3Common${CMAKE_STATIC_LIBRARY_SUFFIX}
    ${VCPKG_INSTALL_PATH}/lib/${CMAKE_STATIC_LIBRARY_PREFIX}BulletCollision${CMAKE_STATIC_LIBRARY_SUFFIX}
    ${VCPKG_INSTALL_PATH}/lib/${CMAKE_STATIC_LIBRARY_PREFIX}BulletDynamics${CMAKE_STATIC_LIBRARY_SUFFIX}
    ${VCPKG_INSTALL_PATH}/lib/${CMAKE_STATIC_LIBRARY_PREFIX}BulletInverseDynamics${CMAKE_STATIC_LIBRARY_SUFFIX}
    ${VCPKG_INSTALL_PATH}/lib/${CMAKE_STATIC_LIBRARY_PREFIX}LinearMath${CMAKE_STATIC_LIBRARY_SUFFIX}
    ${BGFX_LIB}/${CMAKE_STATIC_LIBRARY_PREFIX}bgfx${CMAKE_STATIC_LIBRARY_SUFFIX}
    ${BGFX_LIB}/${CMAKE_STATIC_LIBRARY_PREFIX}astc-codec${CMAKE_STATIC_LIBRARY_SUFFIX}
    ${BGFX_LIB}/${CMAKE_STATIC_LIBRARY_PREFIX}bimg${CMAKE_STATIC_LIBRARY_SUFFIX}
    ${BGFX_LIB}/${CMAKE_STATIC_LIBRARY_PREFIX}bx${CMAKE_STATIC_LIBRARY_SUFFIX}
    ${BGFX_LIB}/${CMAKE_STATIC_LIBRARY_PREFIX}tinyexr${CMAKE_STATIC_LIBRARY_SUFFIX}
    ${PLATFORMLIBS}
       )

    add_compile_definitions(BX_CONFIG_DEBUG=${BX_CONFIG_DEBUG})