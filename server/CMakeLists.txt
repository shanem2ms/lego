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

if (MSVC)
set_target_properties(${SERVER_NAME} PROPERTIES
LINK_FLAGS /SUBSYSTEM:CONSOLE
)
endif ()

find_package(fmt CONFIG REQUIRED)
find_package(libzip CONFIG REQUIRED)
find_package(CURL CONFIG REQUIRED)
find_package(unofficial-enet CONFIG REQUIRED)
find_package(cxxopts CONFIG REQUIRED)
find_package(ZLIB REQUIRED)

if (CMAKE_BUILD_TYPE STREQUAL "Debug")
  set(LIBDBG "debug/")
  set(CMAKE_DEBUG_POSTFIX "_Debug")
endif ()


target_link_libraries(${SERVER_NAME} LINK_PUBLIC 
    fmt::fmt-header-only
    core 
    unofficial::enet::enet
    ZLIB::ZLIB
    leveldb
    libzip::zip
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