
set (BGFX_INCLUDE_ROOT ${CMAKE_INSTALL_PREFIX}/include)
set (BGFX_INCLUDE ${BGFX_INCLUDE_ROOT}/bgfx)
set (BGFX_LIB ${CMAKE_INSTALL_PREFIX}/lib)

if (MSVC)
add_compile_options(/await:strict)
set(BX_INC_DIR_COMPAT ${BGFX_INCLUDE_ROOT}/compat/msvc)
message(CMAKE_CXX_FLAGS ${CMAKE_CXX_FLAGS})
if (${CMAKE_CXX_FLAGS_RELEASE})
STRING(REPLACE "O2" "Od" CMAKE_CXX_FLAGS_RELEASE ${CMAKE_CXX_FLAGS_RELEASE})
endif()
elseif (APPLE)
set(BX_INC_DIR_COMPAT ${BGFX_INCLUDE_ROOT}/compat/osx)
endif()

add_subdirectory(shaders)


################################################################################
# Source groups
################################################################################
set(Header_Files
    "Application.h"
    "Audio.h"
    "BrickMgr.h"
    "World.h"
    "Engine.h"
    "OctTile.h"
    "Hud.h"
    "indexed_map.h"
    "Physics.h"
    "Frustum.h"
    "OctTileSelection.h"
    "ConnectionLogic.h"
    "SceneItem.h"
    "Mesh.h"
    "PlayerView.h"
    "LegoBrick.h"
    "ConnectionWidget.h"
    "LegoUI.h"
    "MbxImport.h"
    "LoresTile.h"
)  

source_group("Header Files" FILES ${Header_Files})

if (SAM_COROUTINE)
set(Coroutine_Files
    "cppcoro/lib/auto_reset_event.cpp"
    "cppcoro/lib/lightweight_manual_reset_event.cpp"
    "cppcoro/lib/spin_mutex.cpp"
    "cppcoro/lib/spin_wait.cpp"
    "cppcoro/lib/static_thread_pool.cpp"
    "cppcoro/lib/win32.cpp"
)
endif()

if (APPLE)
    set (Audio_Src "Audio.mm")
else ()
    set (Audio_Src "Audio.cpp")
endif()

set(Source_Files
    ${Audio_Src}
    "Application.cpp"
    "BrickMgr.cpp"
    "World.cpp"
    "Engine.cpp"
    "OctTile.cpp"
    "Hud.cpp"
    "Frustum.cpp"
    "OctTileSelection.cpp"    
    "SceneItem.cpp"
    "UIControl.cpp"
    "ConvexHull.cpp"
    "Mesh.cpp"
    "GameController.cpp"
    "Physics.cpp"
    "LegoBrick.cpp"
    "PlayerView.cpp"
    "ConnectionWidget.cpp"
    "ConnectionLogic.cpp"
    "LegoUI.cpp"
    "ZipFile.cpp"
    "MbxImport.cpp"
    "LoresTile.cpp"
    "imgui/imgui.cpp"
    "imgui/dear-imgui/ImGuiFileDialog.cpp"
    "imgui/dear-imgui/imgui.cpp"
    "imgui/dear-imgui/imgui_draw.cpp"
    "imgui/dear-imgui/imgui_widgets.cpp"
    "imgui/dear-imgui/imgui_tables.cpp"
    )

source_group("Source Files" FILES ${Source_Files} ${Main_Files})

set(ALL_FILES
    
    ${Header_Files} 
    ${Source_Files}
    ${Coroutine_Files}
    ${Main_Files}
)

find_package(aws-cpp-sdk-core REQUIRED)
add_library(game STATIC ${ALL_FILES} "indexed_map.h")

target_precompile_headers(game PRIVATE "StdIncludes.h")

add_dependencies(game shaders)
################################################################################
# Include directories
################################################################################

message("BGFX_INC_DIR = ${BGFX_INC_DIR}")

target_include_directories(game PUBLIC
    "${CMAKE_CURRENT_SOURCE_DIR}/."
    "${CMAKE_CURRENT_SOURCE_DIR}/imgui/."
    "${CMAKE_CURRENT_SOURCE_DIR}/../core"
    "${CMAKE_CURRENT_SOURCE_DIR}/../core/leveldb/include"
    "${VCPKG_INSTALL_PATH}/include"
    "${VCPKG_INSTALL_PATH}/include/bullet"
    ${BGFX_INCLUDE_ROOT}
    ${BX_INC_DIR_COMPAT}
    aws-cpp-sdk-core
    )

add_compile_definitions(DLLX=;PRId64="I64d";BX_CONFIG_DEBUG=${BX_CONFIG_DEBUG})

file(GLOB LEGOSOUNDS ${CMAKE_SOURCE_DIR}/legosounds/*.wav)

set (All_RESOURCES 
    ${CMAKE_SOURCE_DIR}/hdri/bolonga_irr.dds
    ${CMAKE_SOURCE_DIR}/hdri/bolonga_lod.dds
    ${CMAKE_SOURCE_DIR}/LDConfig.ldr
    ${LEGOSOUNDS}
    )

foreach (RSRC ${All_RESOURCES})

get_filename_component(OUTNAME ${RSRC} NAME)

add_custom_command(
    TARGET game POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy
        ${RSRC}
        ${CMAKE_BINARY_DIR}/${OUTNAME})
    set (OUTRC ${OUTRC}
        ${CMAKE_BINARY_DIR}/${OUTNAME})
endforeach()

set (OUT_RESOURCES ${OUTRC} CACHE INTERNAL "OUT_RESOURCES")

