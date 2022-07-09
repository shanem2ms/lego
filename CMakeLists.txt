cmake_minimum_required(VERSION 3.15.0 FATAL_ERROR)
set(CMAKE_SYSTEM_VERSION 10.0 CACHE STRING "" FORCE)

option(BLOCKO_GAME "Build game" ON)
option(BLOCKO_SERVER "Build Server" ON)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

set(CMAKE_CONFIGURATION_TYPES "Debug;Release") 
project(lego C CXX)

set(PROJECT_NAME lego)
set(SERVER_NAME legosvr)

set (BX_CONFIG_DEBUG 0)
set (BGFX_INCLUDE_ROOT ${CMAKE_INSTALL_PREFIX}/include)
set (BGFX_INCLUDE ${BGFX_INCLUDE_ROOT}/bgfx)
set (BGFX_LIB ${CMAKE_INSTALL_PREFIX}/lib)

set(MainBinaryDir ${CMAKE_BINARY_DIR})
set(BUNDLE_ID "com.lego.shane")

message("vcpkg ${VCPKG_DIR}")

include(${VCPKG_DIR}/scripts/buildsystems/vcpkg.cmake)

add_subdirectory(core)
add_subdirectory(leveldb)
add_dependencies(core leveldb)

if (BLOCKO_GAME)
find_package(bgfx CONFIG REQUIRED )
add_subdirectory(game)
add_dependencies(game core)
add_dependencies(game bgfx::bgfx)
add_dependencies(shaders bgfx::bgfx)

if (APPLE)
add_subdirectory(ios)
add_dependencies(lego game)
else()
add_subdirectory(win)
add_dependencies(lego game)
endif()
endif()

if (BLOCKO_SERVER)
add_subdirectory(server)
endif ()
 
set(CMAKE_XCODE_ATTRIBUTE_PRODUCT_BUNDLE_IDENTIFIER ${BUNDLE_ID})
set(CMAKE_XCODE_ATTRIBUTE_DEVELOPMENT_TEAM "73CP3TPHE9")