
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
)  

source_group("Header Files" FILES ${Header_Files})

set(Source_Files
    "shadercmp.cpp"
    )

source_group("Source Files" FILES ${Source_Files} ${Main_Files})

set(ALL_FILES
    
    ${Header_Files} 
    ${Source_Files}
    ${Main_Files}
)

add_library(shadercmp STATIC ${ALL_FILES})

################################################################################
# Include directories
################################################################################

target_include_directories(shadercmp PUBLIC
    "${CMAKE_CURRENT_SOURCE_DIR}/."
    "${BGFX_INCLUDE_ROOT}"
    "${BX_INC_DIR_COMPAT}"
    "${BGFX_INC_DIR}"
    "${VCPKG_INSTALL_PATH}/include"
    )

add_compile_definitions(DLLX=;PRId64="I64d";BX_CONFIG_DEBUG=${BX_CONFIG_DEBUG})

find_file(SHADERDYN shadercdyn.dll
    PATH ${CMAKE_INSTALL_PREFIX}/bin)

find_file(BGFXSHADERSH bgfx_shader.sh
    PATH ${CMAKE_INSTALL_PREFIX}/include/bgfx)

get_filename_component(OUTNAME ${SHADERDYN} NAME)
add_custom_command(
    TARGET shadercmp POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        ${SHADERDYN}
        ${CMAKE_BINARY_DIR}/${OUTNAME})

get_filename_component(OUTNAME ${BGFXSHADERSH} NAME)
add_custom_command(
    TARGET shadercmp POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        ${BGFXSHADERSH}
        ${CMAKE_BINARY_DIR}/${OUTNAME})