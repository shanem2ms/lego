set (BGFX_INCLUDE_ROOT ${CMAKE_INSTALL_PREFIX}/include)
set (BGFX_INCLUDE ${BGFX_INCLUDE_ROOT}/bgfx)
set (BGFX_LIB ${CMAKE_INSTALL_PREFIX}/lib)

if (MSVC)
add_compile_options(/await:strict)
set(BX_INC_DIR_COMPAT ${BGFX_INCLUDE_ROOT}/compat/msvc)
elseif (APPLE)
set(BX_INC_DIR_COMPAT ${BGFX_INCLUDE_ROOT}/compat/osx)
endif()

add_subdirectory(shaders)
add_subdirectory(fmt)

################################################################################
# Source groups
################################################################################
set(Header_Files
    "StdIncludes.h"
    "Application.h"
    "BrickMgr.h"
    "World.h"
    "Engine.h"
    "OctTile.h"
    "Hud.h"
    "Physics.h"
    "Frustum.h"
    "OctTileSelection.h"
    "SceneItem.h"
    "Mesh.h"
    "PlayerView.h"
    "LegoBrick.h"
    "ConnectionWidget.h"
    "LegoUI.h"
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

set(Source_Files
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
    "PlayerView.cpp"
    "Physics.cpp"
    "Level.cpp"
    "LegoBrick.cpp"
    "ConnectionWidget.cpp"
    "LegoUI.cpp"
    "ldrawloader.cpp"
    "imgui/imgui.cpp"
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

add_library(core STATIC ${ALL_FILES})

target_precompile_headers(core PRIVATE "StdIncludes.h")

add_dependencies(core shaders)
################################################################################
# Include directories
################################################################################

message("BGFX_INC_DIR = ${BGFX_INC_DIR}")

target_include_directories(core PUBLIC
    "${CMAKE_CURRENT_SOURCE_DIR}/."
    "${CMAKE_CURRENT_SOURCE_DIR}/imgui/."
    "${CMAKE_CURRENT_SOURCE_DIR}/../leveldb/include"
    "${CMAKE_CURRENT_SOURCE_DIR}/fmt/include"
    "${VCPKG_INSTALL_PATH}/include"
    ${BGFX_INCLUDE_ROOT}
    ${BX_INC_DIR_COMPAT}
    )

add_compile_definitions(DLLX=;PRId64="I64d";BX_CONFIG_DEBUG=${BX_CONFIG_DEBUG})
