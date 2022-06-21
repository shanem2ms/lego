cmake_minimum_required(VERSION 3.15.0 FATAL_ERROR)
set(CMAKE_SYSTEM_VERSION 10.0 CACHE STRING "" FORCE)
include (ExternalProject)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

set(CMAKE_CONFIGURATION_TYPES "Debug;Release") 
project(lego C CXX)

set(PROJECT_NAME lego)
set(SERVER_NAME legosvr)

set(BGFX_BUILD_EXAMPLES off)
set(BGFX_CUSTOM_TARGETS off)
 
if (APPLE)
    set(BGFX_BUILD_TOOLS off)
endif()

set (BX_CONFIG_DEBUG 0)
set (BGFX_INCLUDE_ROOT ${CMAKE_INSTALL_PREFIX}/include)
set (BGFX_INCLUDE ${BGFX_INCLUDE_ROOT}/bgfx)
set (BGFX_LIB ${CMAKE_INSTALL_PREFIX}/lib)


set(MainBinaryDir ${CMAKE_BINARY_DIR})
set(BUNDLE_ID "com.lego.shane")

message("install path ${CMAKE_INSTALL_PREFIX}")

set(CMAKE_FIND_DEBUG_MODE TRUE)
find_package(bgfx CONFIG REQUIRED )
set(CMAKE_FIND_DEBUG_MODE FALSE)

include(${CMAKE_SOURCE_DIR}/../vcpkg/scripts/buildsystems/vcpkg.cmake)

add_subdirectory(core)
add_subdirectory(zlib)
add_subdirectory(leveldb)
add_dependencies(core zlibstatic)
add_dependencies(core leveldb)

add_subdirectory(game)
add_dependencies(game core)
add_dependencies(game bgfx::bgfx)
add_dependencies(shaders bgfx::bgfx)


if (APPLE)
add_subdirectory(ios)
add_dependencies(lego game)
else()
add_subdirectory(server)
add_subdirectory(win)
add_dependencies(lego game)
endif()
 
set(CMAKE_XCODE_ATTRIBUTE_PRODUCT_BUNDLE_IDENTIFIER ${BUNDLE_ID})
set(CMAKE_XCODE_ATTRIBUTE_DEVELOPMENT_TEAM "73CP3TPHE9")