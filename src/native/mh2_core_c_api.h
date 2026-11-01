#pragma once

#include <cstdint>

#if defined(_WIN32)
#if defined(MH2CORE_EXPORTS)
#define MH2CORE_API __declspec(dllexport)
#else
#define MH2CORE_API __declspec(dllimport)
#endif
#define MH2CORE_CALL __cdecl
#else
#define MH2CORE_API
#define MH2CORE_CALL
#endif

extern "C"
{
    typedef void *Mh2TexHandle;

    struct Mh2TextureInfoNative
    {
        char name[64];
        char format[48];
        uint32_t width;
        uint32_t height;
        uint32_t mipCount;
        uint32_t slotSize;
        int32_t changed;
    };

    MH2CORE_API Mh2TexHandle MH2CORE_CALL mh2_tex_open(const wchar_t *path);
    MH2CORE_API void MH2CORE_CALL mh2_tex_close(Mh2TexHandle handle);
    MH2CORE_API int32_t MH2CORE_CALL mh2_tex_texture_count(Mh2TexHandle handle);
    MH2CORE_API int32_t MH2CORE_CALL mh2_tex_get_texture_info(
        Mh2TexHandle handle,
        int32_t index,
        Mh2TextureInfoNative *info);
    MH2CORE_API int32_t MH2CORE_CALL mh2_tex_render_preview(
        Mh2TexHandle handle,
        int32_t index,
        uint32_t mipLevel,
        int32_t useOriginal,
        int32_t channel,
        uint8_t **pixels,
        uint32_t *width,
        uint32_t *height,
        uint32_t *stride);
    MH2CORE_API void MH2CORE_CALL mh2_tex_free_buffer(void *buffer);
    MH2CORE_API int32_t MH2CORE_CALL mh2_tex_export_png(
        Mh2TexHandle handle,
        int32_t index,
        const wchar_t *path,
        int32_t useOriginal);
    MH2CORE_API int32_t MH2CORE_CALL mh2_tex_replace_png(
        Mh2TexHandle handle,
        int32_t index,
        const wchar_t *path);
    MH2CORE_API int32_t MH2CORE_CALL mh2_tex_revert_texture(Mh2TexHandle handle, int32_t index);
    MH2CORE_API int32_t MH2CORE_CALL mh2_tex_can_undo(Mh2TexHandle handle);
    MH2CORE_API int32_t MH2CORE_CALL mh2_tex_can_redo(Mh2TexHandle handle);
    MH2CORE_API int32_t MH2CORE_CALL mh2_tex_undo(Mh2TexHandle handle);
    MH2CORE_API int32_t MH2CORE_CALL mh2_tex_redo(Mh2TexHandle handle);
    MH2CORE_API int32_t MH2CORE_CALL mh2_tex_is_dirty(Mh2TexHandle handle);
    MH2CORE_API int32_t MH2CORE_CALL mh2_tex_save_as(Mh2TexHandle handle, const wchar_t *path);
    MH2CORE_API int32_t MH2CORE_CALL mh2_get_last_error_utf8(char *buffer, int32_t bufferSize);
}
