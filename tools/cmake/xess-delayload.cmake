# Injected by tools/build-native.ps1 as CMAKE_PROJECT_NRI_INCLUDE for the Upscalers variant, so
# nothing under nri/ is edited. It runs right after NRI's project() call, before the NRI target
# exists, and defers the real work to the end of NRI's top-level CMakeLists.txt.
#
# NRI links libxess.lib, which makes libxess.dll a load-time import of NRI.dll: without the Intel
# runtime beside the application, NRI.dll does not load at all. Delay-loading it moves that failure
# to the first XeSS call (CLAUDE.md: Upscalers and vendor runtimes). Only the NRI target is touched, and only when
# XeSS is compiled in and NRI is a shared library.

function(_prismnri_xess_delayload)
    if(TARGET NRI AND NRI_ENABLE_XESS_SDK AND NOT NRI_STATIC_LIBRARY AND MSVC)
        target_link_options(NRI PRIVATE "/DELAYLOAD:libxess.dll")
        target_link_libraries(NRI PRIVATE delayimp.lib)
        message(STATUS "PrismNRI: NRI links libxess.dll with /DELAYLOAD")
    endif()
endfunction()

cmake_language(DEFER DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}" CALL _prismnri_xess_delayload)
