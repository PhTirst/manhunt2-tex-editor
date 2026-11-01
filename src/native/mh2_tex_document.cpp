#include "mh2_tex_document.hpp"

#include <DirectXTex.h>
#include <Windows.h>
#include <wincodec.h>

#include <algorithm>
#include <cstring>
#include <iomanip>
#include <sstream>
#include <stdexcept>

namespace mh2
{
    namespace
    {
        using namespace DirectX;

        void CheckHresult(HRESULT result, const char *message);

        void EnsureComInitialized()
        {
            thread_local const HRESULT initializationResult = []() {
                const HRESULT result = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
                return result;
            }();
            if (initializationResult != RPC_E_CHANGED_MODE && FAILED(initializationResult))
                CheckHresult(initializationResult, "failed to initialize COM");
        }

        void CheckHresult(HRESULT result, const char *message)
        {
            if (SUCCEEDED(result))
                return;
            std::ostringstream text;
            text << message << " (HRESULT 0x" << std::hex << std::uppercase
                 << static_cast<uint32_t>(result) << ")";
            throw std::runtime_error(text.str());
        }

        std::string FormatName(DXGI_FORMAT format)
        {
            switch (format)
            {
            case DXGI_FORMAT_BC1_UNORM: return "DXT1 / BC1";
            case DXGI_FORMAT_BC1_UNORM_SRGB: return "DXT1 / BC1 sRGB";
            case DXGI_FORMAT_BC2_UNORM: return "DXT3 / BC2";
            case DXGI_FORMAT_BC2_UNORM_SRGB: return "DXT3 / BC2 sRGB";
            case DXGI_FORMAT_BC3_UNORM: return "DXT5 / BC3";
            case DXGI_FORMAT_BC3_UNORM_SRGB: return "DXT5 / BC3 sRGB";
            case DXGI_FORMAT_B8G8R8A8_UNORM: return "BGRA 32-bit";
            case DXGI_FORMAT_B8G8R8A8_UNORM_SRGB: return "BGRA 32-bit sRGB";
            case DXGI_FORMAT_R8G8B8A8_UNORM: return "RGBA 32-bit";
            case DXGI_FORMAT_R8G8B8A8_UNORM_SRGB: return "RGBA 32-bit sRGB";
            case DXGI_FORMAT_B5G6R5_UNORM: return "BGR 565";
            case DXGI_FORMAT_B5G5R5A1_UNORM: return "BGRA 5551";
            case DXGI_FORMAT_B4G4R4A4_UNORM: return "BGRA 4444";
            case DXGI_FORMAT_A8_UNORM: return "Alpha 8-bit";
            default:
                return "DXGI " + std::to_string(static_cast<uint32_t>(format));
            }
        }

        ScratchImage ConvertForDisplay(const Image &source)
        {
            ScratchImage converted;
            const DXGI_FORMAT target = DXGI_FORMAT_B8G8R8A8_UNORM;
            HRESULT result = source.format == target
                ? converted.InitializeFromImage(source)
                : (IsCompressed(source.format)
                    ? Decompress(source, target, converted)
                    : Convert(source, target, TEX_FILTER_DEFAULT, TEX_THRESHOLD_DEFAULT, converted));
            CheckHresult(result, "failed to decode DDS image");
            return converted;
        }

        void ApplyChannel(std::vector<uint8_t> &pixels, PreviewChannel channel)
        {
            if (channel == PreviewChannel::Rgba)
                return;
            for (size_t i = 0; i + 3 < pixels.size(); i += 4)
            {
                uint8_t value = 0;
                switch (channel)
                {
                case PreviewChannel::Alpha: value = pixels[i + 3]; break;
                case PreviewChannel::Red: value = pixels[i + 2]; break;
                case PreviewChannel::Green: value = pixels[i + 1]; break;
                case PreviewChannel::Blue: value = pixels[i]; break;
                default: break;
                }
                pixels[i] = value;
                pixels[i + 1] = value;
                pixels[i + 2] = value;
                pixels[i + 3] = 255;
            }
        }

        std::vector<uint8_t> BlobBytes(const Blob &blob)
        {
            const auto *start = static_cast<const uint8_t *>(blob.GetBufferPointer());
            return std::vector<uint8_t>(start, start + blob.GetBufferSize());
        }
    }

    TexDocument::TexDocument(const std::filesystem::path &path)
    {
        Reload(path);
    }

    const std::filesystem::path &TexDocument::SourcePath() const noexcept
    {
        return sourcePath_;
    }

    size_t TexDocument::TextureCount() const noexcept
    {
        return textures_.size();
    }

    TextureDocumentInfo TexDocument::GetTextureInfo(size_t index) const
    {
        const TextureState &texture = CheckedTexture(index);
        TextureDocumentInfo result;
        result.name = texture.record.name;
        result.format = FormatName(static_cast<DXGI_FORMAT>(texture.dxgiFormat));
        result.width = texture.actualWidth;
        result.height = texture.actualHeight;
        result.mipCount = texture.actualMipCount;
        result.slotSize = texture.record.dataSize;
        result.changed = replacements_.find(index) != replacements_.end();
        return result;
    }

    BgraImage TexDocument::RenderPreview(
        size_t index,
        uint32_t mipLevel,
        bool useOriginal,
        PreviewChannel channel) const
    {
        (void)CheckedTexture(index);
        size_t ddsSize = 0;
        const uint8_t *dds = DdsData(index, useOriginal, ddsSize);

        TexMetadata metadata{};
        ScratchImage source;
        CheckHresult(
            LoadFromDDSMemory(dds, ddsSize, DDS_FLAGS_PERMISSIVE, &metadata, source),
            "failed to decode DDS payload");
        if (mipLevel >= metadata.mipLevels)
            throw std::runtime_error("requested mip level is outside this texture");

        const Image *mip = source.GetImage(mipLevel, 0, 0);
        if (!mip)
            throw std::runtime_error("failed to locate DDS mip level");
        ScratchImage converted = ConvertForDisplay(*mip);
        const Image *image = converted.GetImage(0, 0, 0);
        if (!image)
            throw std::runtime_error("failed to access decoded image");

        BgraImage result;
        result.width = static_cast<uint32_t>(image->width);
        result.height = static_cast<uint32_t>(image->height);
        result.pixels.resize(static_cast<size_t>(result.width) * result.height * 4);
        for (uint32_t y = 0; y < result.height; y++)
        {
            std::memcpy(
                result.pixels.data() + static_cast<size_t>(y) * result.width * 4,
                image->pixels + static_cast<size_t>(y) * image->rowPitch,
                static_cast<size_t>(result.width) * 4);
        }
        ApplyChannel(result.pixels, channel);
        return result;
    }

    void TexDocument::ExportPng(size_t index, const std::filesystem::path &path, bool useOriginal) const
    {
        EnsureComInitialized();
        BgraImage pixels = RenderPreview(index, 0, useOriginal, PreviewChannel::Rgba);
        Image image{};
        image.width = pixels.width;
        image.height = pixels.height;
        image.format = DXGI_FORMAT_B8G8R8A8_UNORM;
        image.rowPitch = static_cast<size_t>(pixels.width) * 4;
        image.slicePitch = image.rowPitch * pixels.height;
        image.pixels = pixels.pixels.data();
        if (!path.parent_path().empty())
            std::filesystem::create_directories(path.parent_path());
        CheckHresult(
            SaveToWICFile(image, WIC_FLAGS_NONE, GUID_ContainerFormatPng, path.c_str()),
            "failed to save PNG");
    }

    void TexDocument::ReplaceFromPng(size_t index, const std::filesystem::path &path)
    {
        std::vector<uint8_t> replacement = EncodeReplacementPng(index, path);
        std::optional<std::vector<uint8_t>> before;
        const auto existing = replacements_.find(index);
        if (existing != replacements_.end())
            before = existing->second;
        PushAction(EditAction{index, std::move(before), std::move(replacement)});
    }

    void TexDocument::RevertTexture(size_t index)
    {
        (void)CheckedTexture(index);
        const auto existing = replacements_.find(index);
        if (existing == replacements_.end())
            return;
        PushAction(EditAction{index, existing->second, std::nullopt});
    }

    bool TexDocument::CanUndo() const noexcept
    {
        return historyPosition_ > 0;
    }

    bool TexDocument::CanRedo() const noexcept
    {
        return historyPosition_ < history_.size();
    }

    void TexDocument::Undo()
    {
        if (!CanUndo())
            return;
        historyPosition_--;
        const EditAction &action = history_[historyPosition_];
        ApplyState(action.textureIndex, action.before);
    }

    void TexDocument::Redo()
    {
        if (!CanRedo())
            return;
        const EditAction &action = history_[historyPosition_];
        ApplyState(action.textureIndex, action.after);
        historyPosition_++;
    }

    bool TexDocument::IsDirty() const noexcept
    {
        return !replacements_.empty();
    }

    void TexDocument::SaveAs(const std::filesystem::path &path)
    {
        if (path.empty())
            throw std::runtime_error("output TEX path is empty");
        if (!path.parent_path().empty())
            std::filesystem::create_directories(path.parent_path());

        std::map<size_t, std::vector<uint8_t>> replacementsByRecordOffset;
        for (const auto &item : replacements_)
            replacementsByRecordOffset[textures_.at(item.first).record.recordOffset] = item.second;

        std::filesystem::path temp = path;
        temp += L".mh2tex.tmp";
        std::error_code ignored;
        std::filesystem::remove(temp, ignored);
        try
        {
            WriteTexReplacingDdsByRecordOffset(sourcePath_, temp, replacementsByRecordOffset);
            if (!MoveFileExW(temp.c_str(), path.c_str(), MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
                throw std::runtime_error("failed to atomically replace output TEX");
        }
        catch (...)
        {
            std::filesystem::remove(temp, ignored);
            throw;
        }
        Reload(path);
    }

    void TexDocument::Reload(const std::filesystem::path &path)
    {
        sourcePath_ = std::filesystem::absolute(path);
        tex_ = ReadTex(sourcePath_);
        textures_.clear();
        textures_.reserve(tex_.records.size());

        for (const TexTextureRecord &record : tex_.records)
        {
            TexMetadata metadata{};
            ScratchImage image;
            const uint8_t *dds = tex_.decompressed.data() + record.dataOffset;
            CheckHresult(
                LoadFromDDSMemory(dds, record.dataSize, DDS_FLAGS_PERMISSIVE, &metadata, image),
                ("failed to read DDS metadata for " + record.name).c_str());

            TextureState state;
            state.record = record;
            state.actualWidth = static_cast<uint32_t>(metadata.width);
            state.actualHeight = static_cast<uint32_t>(metadata.height);
            state.actualMipCount = static_cast<uint32_t>(std::max<size_t>(1, metadata.mipLevels));
            state.dxgiFormat = static_cast<uint32_t>(metadata.format);
            textures_.push_back(std::move(state));
        }

        replacements_.clear();
        history_.clear();
        historyPosition_ = 0;
    }

    const TexDocument::TextureState &TexDocument::CheckedTexture(size_t index) const
    {
        if (index >= textures_.size())
            throw std::runtime_error("texture index is outside the document");
        return textures_[index];
    }

    const uint8_t *TexDocument::DdsData(size_t index, bool useOriginal, size_t &size) const
    {
        const TextureState &texture = CheckedTexture(index);
        if (!useOriginal)
        {
            const auto replacement = replacements_.find(index);
            if (replacement != replacements_.end())
            {
                size = replacement->second.size();
                return replacement->second.data();
            }
        }
        size = texture.record.dataSize;
        return tex_.decompressed.data() + texture.record.dataOffset;
    }

    std::vector<uint8_t> TexDocument::EncodeReplacementPng(
        size_t index,
        const std::filesystem::path &path) const
    {
        EnsureComInitialized();
        const TextureState &texture = CheckedTexture(index);

        TexMetadata pngMetadata{};
        ScratchImage png;
        CheckHresult(
            LoadFromWICFile(path.c_str(), WIC_FLAGS_FORCE_RGB, &pngMetadata, png),
            "failed to load replacement PNG");
        if (pngMetadata.width != texture.actualWidth || pngMetadata.height != texture.actualHeight)
        {
            throw std::runtime_error(
                "PNG dimensions " + std::to_string(pngMetadata.width) + "x" + std::to_string(pngMetadata.height) +
                " do not match the TEX texture " + std::to_string(texture.actualWidth) + "x" +
                std::to_string(texture.actualHeight));
        }

        const Image *base = png.GetImage(0, 0, 0);
        if (!base)
            throw std::runtime_error("replacement PNG has no image data");

        ScratchImage rgba;
        const HRESULT convertResult = base->format == DXGI_FORMAT_R8G8B8A8_UNORM
            ? rgba.InitializeFromImage(*base)
            : Convert(*base, DXGI_FORMAT_R8G8B8A8_UNORM, TEX_FILTER_DEFAULT, TEX_THRESHOLD_DEFAULT, rgba);
        CheckHresult(convertResult, "failed to convert replacement PNG");

        ScratchImage mipChain;
        const size_t mipLevels = std::max<uint32_t>(1, texture.actualMipCount);
        const HRESULT mipResult = mipLevels == 1
            ? mipChain.InitializeFromImage(*rgba.GetImage(0, 0, 0))
            : GenerateMipMaps(*rgba.GetImage(0, 0, 0), TEX_FILTER_FANT, mipLevels, mipChain);
        CheckHresult(mipResult, "failed to generate replacement mipmaps");

        const DXGI_FORMAT targetFormat = static_cast<DXGI_FORMAT>(texture.dxgiFormat);
        ScratchImage encoded;
        if (IsCompressed(targetFormat))
        {
            const TEX_COMPRESS_FLAGS flags = static_cast<TEX_COMPRESS_FLAGS>(
                TEX_COMPRESS_DITHER);
            CheckHresult(
                Compress(
                    mipChain.GetImages(),
                    mipChain.GetImageCount(),
                    mipChain.GetMetadata(),
                    targetFormat,
                    flags,
                    TEX_THRESHOLD_DEFAULT,
                    encoded),
                "failed to compress replacement DDS");
        }
        else if (mipChain.GetMetadata().format != targetFormat)
        {
            CheckHresult(
                Convert(
                    mipChain.GetImages(),
                    mipChain.GetImageCount(),
                    mipChain.GetMetadata(),
                    targetFormat,
                    TEX_FILTER_DEFAULT,
                    TEX_THRESHOLD_DEFAULT,
                    encoded),
                "failed to encode replacement DDS");
        }

        Blob blob;
        const ScratchImage &finalImage = encoded.GetImageCount() == 0 ? mipChain : encoded;
        CheckHresult(
            SaveToDDSMemory(
                finalImage.GetImages(),
                finalImage.GetImageCount(),
                finalImage.GetMetadata(),
                DDS_FLAGS_NONE,
                blob),
            "failed to serialize replacement DDS");
        std::vector<uint8_t> result = BlobBytes(blob);
        if (result.size() > texture.record.dataSize)
        {
            throw std::runtime_error(
                "encoded DDS is " + std::to_string(result.size()) + " bytes, but TEX slot " +
                texture.record.name + " only allows " + std::to_string(texture.record.dataSize) + " bytes");
        }
        return result;
    }

    void TexDocument::PushAction(EditAction action)
    {
        if (historyPosition_ < history_.size())
            history_.erase(history_.begin() + static_cast<std::ptrdiff_t>(historyPosition_), history_.end());
        ApplyState(action.textureIndex, action.after);
        history_.push_back(std::move(action));
        historyPosition_ = history_.size();
    }

    void TexDocument::ApplyState(size_t index, const std::optional<std::vector<uint8_t>> &state)
    {
        (void)CheckedTexture(index);
        if (state)
            replacements_[index] = *state;
        else
            replacements_.erase(index);
    }
}
