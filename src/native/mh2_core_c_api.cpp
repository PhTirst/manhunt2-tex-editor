#include "mh2_core_c_api.h"

#include "mh2_tex_document.hpp"

#include <algorithm>
#include <cstring>
#include <exception>
#include <memory>
#include <stdexcept>
#include <string>

namespace
{
    thread_local std::string lastError;

    mh2::TexDocument &Document(Mh2TexHandle handle)
    {
        if (!handle)
            throw std::runtime_error("TEX document is not open");
        return *static_cast<mh2::TexDocument *>(handle);
    }

    size_t CheckedIndex(int32_t index)
    {
        if (index < 0)
            throw std::runtime_error("texture index cannot be negative");
        return static_cast<size_t>(index);
    }

    template <typename Action>
    int32_t Guard(Action action)
    {
        try
        {
            action();
            lastError.clear();
            return 1;
        }
        catch (const std::exception &error)
        {
            lastError = error.what();
            return 0;
        }
        catch (...)
        {
            lastError = "unknown native error";
            return 0;
        }
    }

    template <size_t Size>
    void CopyText(char (&target)[Size], const std::string &source)
    {
        const size_t count = std::min(source.size(), Size - 1);
        std::memcpy(target, source.data(), count);
        target[count] = '\0';
    }
}

extern "C"
{
    Mh2TexHandle MH2CORE_CALL mh2_tex_open(const wchar_t *path)
    {
        if (!path)
        {
            lastError = "TEX path is null";
            return nullptr;
        }
        try
        {
            auto document = std::make_unique<mh2::TexDocument>(path);
            lastError.clear();
            return document.release();
        }
        catch (const std::exception &error)
        {
            lastError = error.what();
            return nullptr;
        }
        catch (...)
        {
            lastError = "unknown native error";
            return nullptr;
        }
    }

    void MH2CORE_CALL mh2_tex_close(Mh2TexHandle handle)
    {
        delete static_cast<mh2::TexDocument *>(handle);
    }

    int32_t MH2CORE_CALL mh2_tex_texture_count(Mh2TexHandle handle)
    {
        try
        {
            const size_t count = Document(handle).TextureCount();
            if (count > static_cast<size_t>(INT32_MAX))
                throw std::runtime_error("TEX contains too many textures");
            lastError.clear();
            return static_cast<int32_t>(count);
        }
        catch (const std::exception &error)
        {
            lastError = error.what();
            return -1;
        }
    }

    int32_t MH2CORE_CALL mh2_tex_get_texture_info(
        Mh2TexHandle handle,
        int32_t index,
        Mh2TextureInfoNative *info)
    {
        return Guard([&]() {
            if (!info)
                throw std::runtime_error("texture info output is null");
            const mh2::TextureDocumentInfo source = Document(handle).GetTextureInfo(CheckedIndex(index));
            *info = Mh2TextureInfoNative{};
            CopyText(info->name, source.name);
            CopyText(info->format, source.format);
            info->width = source.width;
            info->height = source.height;
            info->mipCount = source.mipCount;
            info->slotSize = source.slotSize;
            info->changed = source.changed ? 1 : 0;
        });
    }

    int32_t MH2CORE_CALL mh2_tex_render_preview(
        Mh2TexHandle handle,
        int32_t index,
        uint32_t mipLevel,
        int32_t useOriginal,
        int32_t channel,
        uint8_t **pixels,
        uint32_t *width,
        uint32_t *height,
        uint32_t *stride)
    {
        return Guard([&]() {
            if (!pixels || !width || !height || !stride)
                throw std::runtime_error("preview output pointer is null");
            if (channel < 0 || channel > 4)
                throw std::runtime_error("preview channel is invalid");
            mh2::BgraImage image = Document(handle).RenderPreview(
                CheckedIndex(index),
                mipLevel,
                useOriginal != 0,
                static_cast<mh2::PreviewChannel>(channel));
            auto output = std::make_unique<uint8_t[]>(image.pixels.size());
            std::memcpy(output.get(), image.pixels.data(), image.pixels.size());
            *pixels = output.release();
            *width = image.width;
            *height = image.height;
            *stride = image.width * 4;
        });
    }

    void MH2CORE_CALL mh2_tex_free_buffer(void *buffer)
    {
        delete[] static_cast<uint8_t *>(buffer);
    }

    int32_t MH2CORE_CALL mh2_tex_export_png(
        Mh2TexHandle handle,
        int32_t index,
        const wchar_t *path,
        int32_t useOriginal)
    {
        return Guard([&]() {
            if (!path)
                throw std::runtime_error("PNG output path is null");
            Document(handle).ExportPng(CheckedIndex(index), path, useOriginal != 0);
        });
    }

    int32_t MH2CORE_CALL mh2_tex_replace_png(
        Mh2TexHandle handle,
        int32_t index,
        const wchar_t *path)
    {
        return Guard([&]() {
            if (!path)
                throw std::runtime_error("PNG input path is null");
            Document(handle).ReplaceFromPng(CheckedIndex(index), path);
        });
    }

    int32_t MH2CORE_CALL mh2_tex_revert_texture(Mh2TexHandle handle, int32_t index)
    {
        return Guard([&]() { Document(handle).RevertTexture(CheckedIndex(index)); });
    }

    int32_t MH2CORE_CALL mh2_tex_can_undo(Mh2TexHandle handle)
    {
        try { return Document(handle).CanUndo() ? 1 : 0; }
        catch (...) { return 0; }
    }

    int32_t MH2CORE_CALL mh2_tex_can_redo(Mh2TexHandle handle)
    {
        try { return Document(handle).CanRedo() ? 1 : 0; }
        catch (...) { return 0; }
    }

    int32_t MH2CORE_CALL mh2_tex_undo(Mh2TexHandle handle)
    {
        return Guard([&]() { Document(handle).Undo(); });
    }

    int32_t MH2CORE_CALL mh2_tex_redo(Mh2TexHandle handle)
    {
        return Guard([&]() { Document(handle).Redo(); });
    }

    int32_t MH2CORE_CALL mh2_tex_is_dirty(Mh2TexHandle handle)
    {
        try { return Document(handle).IsDirty() ? 1 : 0; }
        catch (...) { return 0; }
    }

    int32_t MH2CORE_CALL mh2_tex_save_as(Mh2TexHandle handle, const wchar_t *path)
    {
        return Guard([&]() {
            if (!path)
                throw std::runtime_error("TEX output path is null");
            Document(handle).SaveAs(path);
        });
    }

    int32_t MH2CORE_CALL mh2_get_last_error_utf8(char *buffer, int32_t bufferSize)
    {
        if (!buffer || bufferSize <= 0)
            return static_cast<int32_t>(lastError.size() + 1);
        const size_t capacity = static_cast<size_t>(bufferSize);
        const size_t count = std::min(lastError.size(), capacity - 1);
        std::memcpy(buffer, lastError.data(), count);
        buffer[count] = '\0';
        return static_cast<int32_t>(lastError.size() + 1);
    }
}
