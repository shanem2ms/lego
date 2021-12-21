cmake_minimum_required(VERSION 3.15.0 FATAL_ERROR)
set(CMAKE_SYSTEM_VERSION 10.0 CACHE STRING "" FORCE)
include(/homep4/vcpkg/scripts/buildsystems/vcpkg.cmake)

set (BGFX_INCLUDE_ROOT ${CMAKE_INSTALL_PREFIX}/include)
set (BGFX_INCLUDE ${BGFX_INCLUDE_ROOT}/bgfx)
set (BGFX_LIB ${CMAKE_INSTALL_PREFIX}/lib)
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

target_link_libraries(${PROJECT_NAME} LINK_PUBLIC 
    ${BGFX_LIB}/bgfx.lib 
    ${BGFX_LIB}/astc-codec.lib 
    ${BGFX_LIB}/bimg.lib 
    ${BGFX_LIB}/bx.lib
    ${VCPKG_INSTALL_PATH}/debug/lib/Bullet3Common_Debug.lib
    ${VCPKG_INSTALL_PATH}/debug/lib/BulletCollision_Debug.lib
    ${VCPKG_INSTALL_PATH}/debug/lib/BulletDynamics_Debug.lib
    ${VCPKG_INSTALL_PATH}/debug/lib/BulletInverseDynamics_Debug.lib
    ${VCPKG_INSTALL_PATH}/debug/lib/LinearMath_Debug.lib
    fmt
    core 
    ${PLATFORMLIBS}
    zlibstatic
    leveldb
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