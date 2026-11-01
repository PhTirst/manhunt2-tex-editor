#pragma once

#include "mh2_tex.hpp"

#include <cstddef>
#include <cstdint>
#include <filesystem>
#include <map>
#include <optional>
#include <string>
#include <vector>

namespace mh2
{
    enum class PreviewChannel
    {
        Rgba = 0,
        Alpha = 1,
        Red = 2,
        Green = 3,
        Blue = 4,
    };

    struct TextureDocumentInfo
    {
        std::string name;
        std::string format;
        uint32_t width = 0;
        uint32_t height = 0;
        uint32_t mipCount = 0;
        uint32_t slotSize = 0;
        bool changed = false;
    };

    struct BgraImage
    {
        uint32_t width = 0;
        uint32_t height = 0;
        std::vector<uint8_t> pixels;
    };

    class TexDocument
    {
    public:
        explicit TexDocument(const std::filesystem::path &path);

        const std::filesystem::path &SourcePath() const noexcept;
        size_t TextureCount() const noexcept;
        TextureDocumentInfo GetTextureInfo(size_t index) const;
        BgraImage RenderPreview(
            size_t index,
            uint32_t mipLevel,
            bool useOriginal,
            PreviewChannel channel) const;
        void ExportPng(size_t index, const std::filesystem::path &path, bool useOriginal) const;
        void ReplaceFromPng(size_t index, const std::filesystem::path &path);
        void RevertTexture(size_t index);
        bool CanUndo() const noexcept;
        bool CanRedo() const noexcept;
        void Undo();
        void Redo();
        bool IsDirty() const noexcept;
        void SaveAs(const std::filesystem::path &path);

    private:
        struct TextureState
        {
            TexTextureRecord record;
            uint32_t actualWidth = 0;
            uint32_t actualHeight = 0;
            uint32_t actualMipCount = 0;
            uint32_t dxgiFormat = 0;
        };

        struct EditAction
        {
            size_t textureIndex = 0;
            std::optional<std::vector<uint8_t>> before;
            std::optional<std::vector<uint8_t>> after;
        };

        std::filesystem::path sourcePath_;
        TexFile tex_;
        std::vector<TextureState> textures_;
        std::map<size_t, std::vector<uint8_t>> replacements_;
        std::vector<EditAction> history_;
        size_t historyPosition_ = 0;

        void Reload(const std::filesystem::path &path);
        const TextureState &CheckedTexture(size_t index) const;
        const uint8_t *DdsData(size_t index, bool useOriginal, size_t &size) const;
        std::vector<uint8_t> EncodeReplacementPng(size_t index, const std::filesystem::path &path) const;
        void PushAction(EditAction action);
        void ApplyState(size_t index, const std::optional<std::vector<uint8_t>> &state);
    };
}
