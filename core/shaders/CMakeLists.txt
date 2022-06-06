set (VS_SHADERS vs_brick.sc vs_connector.sc vs_hud.sc vs_physicsdbg.sc vs_cubes.sc vs_fullscreen.sc
    vs_frustum.sc vs_gamecontroller.sc vs_brickpreview.sc) 
set (FS_SHADERS fs_cubes.sc fs_pickconnector.sc fs_pickbrick.sc fs_brickpreview.sc fs_frustum.sc 
    fs_forwardshade.sc fs_hud.sc fs_bbox.sc fs_deferred.sc fs_blit.sc fs_ibl.sc fs_gamecontroller.sc)

set (CS_BINS ${CS_SHADERS})
list(TRANSFORM CS_BINS REPLACE "\\.sc" ".bin" )
list(TRANSFORM CS_BINS PREPEND ${CMAKE_BINARY_DIR}/ )

set (FS_BINS ${FS_SHADERS})
list(TRANSFORM FS_BINS REPLACE "\\.sc" ".bin" )
list(TRANSFORM FS_BINS PREPEND ${CMAKE_BINARY_DIR}/ )

set (VS_BINS ${VS_SHADERS})
list(TRANSFORM VS_BINS REPLACE "\\.sc" ".bin" )
list(TRANSFORM VS_BINS PREPEND ${CMAKE_BINARY_DIR}/ )

message(${VS_BINS})
message(${FS_BINS})


set(ALL_FILES        
    ${FS_BINS}
    ${CS_BINS}
    ${VS_BINS}
)

set (ALL_SHADERS ${ALL_FILES} CACHE INTERNAL "ALL_SHADERS")

if (MSVC)
set (SHADERC ${CMAKE_INSTALL_PREFIX}/bin/shaderc.exe)
set (SHADERC_PLATFORM windows)
set (SHADERC_VPROF vs_4_0)
set (SHADERC_FPROF ps_4_0)
set (SHADERC_CPROF cs_5_0)
elseif (APPLE)
find_file(SHADERC shaderc ${CMAKE_SOURCE_DIR}/ios)
set (SHADERC_PLATFORM osx)
set (SHADERC_VPROF metal)
set (SHADERC_FPROF metal)
set (SHADERC_CPROF metal)
endif()

set (BGFX_SRC_DIR ${CMAKE_SOURCE_DIR}/bgfx.cmake/bgfx/src)


add_custom_target(shaders ALL DEPENDS ${ALL_FILES})

foreach (SHDR ${FS_SHADERS})
string(REPLACE "\.sc" ".bin" SHDRBIN ${SHDR})

message ("FS ${CMAKE_BINARY_DIR}/${SHDRBIN}")

set (SHADERDBGON --debug -O 0)


add_custom_command(
  OUTPUT ${CMAKE_BINARY_DIR}/${SHDRBIN}
  COMMAND ${SHADERC} -f ${CMAKE_CURRENT_SOURCE_DIR}/${SHDR} --varyingdef ${CMAKE_CURRENT_SOURCE_DIR}/varying.def.sc
                     -o ${CMAKE_BINARY_DIR}/${SHDRBIN} --type f --platform ${SHADERC_PLATFORM} --profile ${SHADERC_FPROF} -i ${BGFX_INCLUDE} ${SHADERDBG}
  DEPENDS ${CMAKE_CURRENT_SOURCE_DIR}/${SHDR} ${CMAKE_CURRENT_SOURCE_DIR}/uniforms.sh ${CMAKE_CURRENT_SOURCE_DIR}/varying.def.sc
  VERBATIM)
  
endforeach()

foreach (SHDR ${CS_SHADERS})

string(REPLACE "\.sc" ".bin" SHDRBIN ${SHDR})

add_custom_command(
  OUTPUT ${CMAKE_BINARY_DIR}/${SHDRBIN}
  COMMAND ${SHADERC} -f ${CMAKE_CURRENT_SOURCE_DIR}/${SHDR} --varyingdef ${CMAKE_CURRENT_SOURCE_DIR}/varying.def.sc
                     -o ${CMAKE_BINARY_DIR}/${SHDRBIN} --type c --platform ${SHADERC_PLATFORM} --profile ${SHADERC_CPROF} -i ${BGFX_INCLUDE} ${SHADERDBG}
  DEPENDS ${CMAKE_CURRENT_SOURCE_DIR}/${SHDR} ${CMAKE_CURRENT_SOURCE_DIR}/uniforms.sh ${CMAKE_CURRENT_SOURCE_DIR}/varying.def.sc
  VERBATIM)
  
endforeach()

foreach (SHDR ${VS_SHADERS})

string(REPLACE "\.sc" ".bin" SHDRBIN ${SHDR})

add_custom_command(
  OUTPUT ${CMAKE_BINARY_DIR}/${SHDRBIN}
  COMMAND ${SHADERC} -f ${CMAKE_CURRENT_SOURCE_DIR}/${SHDR} --varyingdef ${CMAKE_CURRENT_SOURCE_DIR}/varying.def.sc
                     -o ${CMAKE_BINARY_DIR}/${SHDRBIN} --type v --platform ${SHADERC_PLATFORM} --profile ${SHADERC_VPROF} -i ${BGFX_INCLUDE} ${SHADERDBG}
  DEPENDS ${CMAKE_CURRENT_SOURCE_DIR}/${SHDR} ${CMAKE_CURRENT_SOURCE_DIR}/uniforms.sh ${CMAKE_CURRENT_SOURCE_DIR}/varying.def.sc
  VERBATIM)
  
endforeach()


