cmake_minimum_required(VERSION 3.15.0 FATAL_ERROR)
set(CMAKE_SYSTEM_VERSION 10.0 CACHE STRING "" FORCE)

set (BGFX_INCLUDE_ROOT ${CMAKE_INSTALL_PREFIX}/include)
set (BGFX_INCLUDE ${BGFX_INCLUDE_ROOT}/bgfx)
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${MainBinaryDir})

################################################################################
# Target
################################################################################
set(Main_Files
"server.cpp"
)

add_executable(${SERVER_NAME} WIN32 ${Main_Files})

set_target_properties(${SERVER_NAME} PROPERTIES
LINK_FLAGS /SUBSYSTEM:CONSOLE
)
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
find_package(aws-cpp-sdk-core REQUIRED)
find_package(aws-cpp-sdk-s3 REQUIRED)
find_package(bsoncxx CONFIG REQUIRED)
find_package(mongocxx CONFIG REQUIRED)
find_package(unofficial-enet CONFIG REQUIRED)
find_package(cxxopts CONFIG REQUIRED)

if (CMAKE_BUILD_TYPE STREQUAL "Debug")
  set(LIBDBG "debug/")
  set(CMAKE_DEBUG_POSTFIX "_Debug")
endif ()


target_link_libraries(${SERVER_NAME} LINK_PUBLIC 
    fmt::fmt-header-only
    core 
    ${PLATFORMLIBS}
    unofficial::enet::enet
    zlibstatic
    leveldb
    libzip::zip
    aws-cpp-sdk-core
    aws-cpp-sdk-s3
    cxxopts::cxxopts
       )
       
target_include_directories(${SERVER_NAME} PUBLIC
    "${CMAKE_CURRENT_SOURCE_DIR}/."
    "${CMAKE_BINARY_DIR}/."
    "${BGFX_INCLUDE_ROOT}"
    "${BX_INC_DIR_COMPAT}"
    "${BGFX_INC_DIR}"
    "${CMAKE_CURRENT_SOURCE_DIR}/leveldb/include"
    )
    
add_compile_definitions(BX_CONFIG_DEBUG=${BX_CONFIG_DEBUG})