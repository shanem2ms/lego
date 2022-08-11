cmake_minimum_required(VERSION 3.15.0 FATAL_ERROR)
set(CMAKE_SYSTEM_VERSION 10.0 CACHE STRING "" FORCE)

set (BGFX_INCLUDE_ROOT ${CMAKE_INSTALL_PREFIX}/include)
set (BGFX_INCLUDE ${BGFX_INCLUDE_ROOT}/bgfx)
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${MainBinaryDir})

################################################################################
# Target
################################################################################
set(Main_Files
"map.cpp"
"Resource.h"
"map.h"
"map.rc"
)

add_executable(${PROJECT_NAME} WIN32 ${Main_Files})

set (PLATFORMLIBS
"d3dcompiler;"
"dxgi;"
"dxguid;"
"d3d11;"
"Shcore;")

find_package(Bullet CONFIG REQUIRED)
find_package(fmt CONFIG REQUIRED)
find_package(libzip CONFIG REQUIRED)
find_package(CURL CONFIG REQUIRED)
find_package(unofficial-enet CONFIG REQUIRED)
find_package(ZLIB REQUIRED)
find_package(cxxopts CONFIG REQUIRED)
find_path(RAPIDXML_INCLUDE_DIRS "rapidxml/rapidxml.hpp" REQUIRED_FILES)
    
if (CMAKE_BUILD_TYPE STREQUAL "Debug")
  set(LIBDBG "debug/")
  set(CMAKE_DEBUG_POSTFIX "_Debug")
endif ()

target_link_libraries(${PROJECT_NAME} LINK_PUBLIC 
    bgfx::bgfx
    bgfx::bimg
    bgfx::bx
    ${VCPKG_INSTALL_PATH}/${LIBDBG}lib/Bullet3Common${CMAKE_DEBUG_POSTFIX}.lib
    ${VCPKG_INSTALL_PATH}/${LIBDBG}lib/BulletCollision${CMAKE_DEBUG_POSTFIX}.lib
    ${VCPKG_INSTALL_PATH}/${LIBDBG}lib/BulletDynamics${CMAKE_DEBUG_POSTFIX}.lib
    ${VCPKG_INSTALL_PATH}/${LIBDBG}lib/BulletInverseDynamics${CMAKE_DEBUG_POSTFIX}.lib
    ${VCPKG_INSTALL_PATH}/${LIBDBG}lib/LinearMath${CMAKE_DEBUG_POSTFIX}.lib
    unofficial::enet::enet
    fmt::fmt-header-only
    CURL::libcurl
    core 
    shadercmp
    game
    ${PLATFORMLIBS}
    ZLIB::ZLIB
    leveldb
    libzip::zip
    cxxopts::cxxopts
       )
       
target_include_directories(${PROJECT_NAME} PUBLIC
    "${CMAKE_CURRENT_SOURCE_DIR}/."
    "${CMAKE_BINARY_DIR}/."
    "${BGFX_INCLUDE_ROOT}"
    "${BX_INC_DIR_COMPAT}"
    "${BGFX_INC_DIR}"
    "${CMAKE_CURRENT_SOURCE_DIR}/leveldb/include"
    )
    
add_compile_definitions(BX_CONFIG_DEBUG=${BX_CONFIG_DEBUG})
