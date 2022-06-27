
set (BGFX_INCLUDE_ROOT ${CMAKE_INSTALL_PREFIX}/include)
set (BGFX_INCLUDE ${BGFX_INCLUDE_ROOT}/bgfx)
set (BGFX_LIB ${CMAKE_INSTALL_PREFIX}/lib)

if (MSVC)
add_compile_options(/await:strict)
set(BX_INC_DIR_COMPAT ${BGFX_INCLUDE_ROOT}/compat/msvc)
if (${CMAKE_CXX_FLAGS_RELEASE})
STRING(REPLACE "O2" "Od" CMAKE_CXX_FLAGS_RELEASE ${CMAKE_CXX_FLAGS_RELEASE})
endif()
elseif (APPLE)
set(BX_INC_DIR_COMPAT ${BGFX_INCLUDE_ROOT}/compat/osx)
endif()

find_package(nlohmann_json CONFIG REQUIRED)

################################################################################
# Source groups
################################################################################
set(Header_Files
    "StdIncludes.h"
    "Enet.h"
    "Level.h"
    "Server.h"
)  

source_group("Header Files" FILES ${Header_Files})

set(Source_Files
    "Enet.cpp"
    "Level.cpp"
    "Server.cpp"
    )

source_group("Source Files" FILES ${Source_Files} ${Main_Files})

set(ALL_FILES
    
    ${Header_Files} 
    ${Source_Files}
    ${Main_Files}
)

add_library(core STATIC ${ALL_FILES})
target_precompile_headers(core PRIVATE "StdIncludes.h")

################################################################################
# Include directories
################################################################################

target_include_directories(core PUBLIC
    "${CMAKE_CURRENT_SOURCE_DIR}/."
    "${CMAKE_CURRENT_SOURCE_DIR}/../leveldb/include"
    "${VCPKG_INSTALL_PATH}/include"
    )

add_compile_definitions(DLLX=;PRId64="I64d";BX_CONFIG_DEBUG=${BX_CONFIG_DEBUG})

