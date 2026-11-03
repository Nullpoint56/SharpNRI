# Injected by tools/build-native.ps1 as CMAKE_PROJECT_NRI_INCLUDE for every Windows build, so
# nothing under nri/ is edited. It runs right after NRI's project() call, before the NRI target
# exists, and defers the real work to the end of NRI's top-level CMakeLists.txt.
#
# d3d12.dll and dxgi.dll: NRI links d3d12.lib and dxgi.lib, which makes both load-time imports of
# NRI.dll, so every process that loads NRI.dll loads them, a Vulkan-only one included. Delay-loading
# them defers the load to the first D3D12 or DXGI call (D3D12GetInterface in patch 0001, the D3D
# pass of an adapter enumeration, a swap chain's display query). CLAUDE.md: Device creation and
# callbacks.
#
# libxess.dll: NRI links libxess.lib when XeSS is compiled in, which makes libxess.dll a load-time
# import: without the Intel runtime beside the application, NRI.dll does not load at all.
# Delay-loading it moves that failure to the first XeSS call (CLAUDE.md: Upscalers and vendor
# runtimes).
#
# Only the NRI target is touched, and only when NRI is a shared library built by MSVC.

function(_sharpnri_delayload)
    if(TARGET NRI AND NOT NRI_STATIC_LIBRARY AND MSVC)
        set(_dlls "")
        if(NRI_ENABLE_D3D12_SUPPORT)
            list(APPEND _dlls d3d12.dll dxgi.dll)
        endif()
        if(NRI_ENABLE_XESS_SDK)
            list(APPEND _dlls libxess.dll)
        endif()
        if(_dlls)
            foreach(_dll IN LISTS _dlls)
                target_link_options(NRI PRIVATE "/DELAYLOAD:${_dll}")
            endforeach()
            target_link_libraries(NRI PRIVATE delayimp.lib)
            list(JOIN _dlls ", " _joined)
            message(STATUS "SharpNRI: NRI links ${_joined} with /DELAYLOAD")
        endif()
    endif()
endfunction()

cmake_language(DEFER DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}" CALL _sharpnri_delayload)
