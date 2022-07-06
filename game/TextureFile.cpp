/// \author SMorriso

// wxint64: Used for long integer values (file position)
#include "TextureFile.h"

namespace sam
{
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    std::shared_ptr<TextureFileReader> TextureFileReader::Create(const std::string& inFileName)
    {
        return std::make_shared<TextureFileReader>(inFileName);
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////

    TextureFileReader::TextureFileReader(const std::string& inFileName) :
        m_inFileName(inFileName)
    {
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////

    uint32_t BitsPerPixel(TextureFileHeader::Format fmt)
    {
        switch (fmt)
        {
        case TextureFileHeader::Format::eRGBA128Bit:
            return 32;
        case TextureFileHeader::Format::eRGBA64Bit:
            return 16;
        default:
            return 8;
        }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////

    uint32_t ComponentCount(TextureFileHeader::Format fmt)
    {
        return 4;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////

    std::shared_ptr<TextureFileReader::ImageInfo> TextureFileReader::Read(
        std::ifstream& file, uint32_t startOffset, bool relativeOffsets)
    {
        TextureFileHeader fileHeader;
        std::shared_ptr<TextureFileReader::ImageInfo> rImageInfo = ReadHeader(file, startOffset, fileHeader);

        rImageInfo->m_data.reserve(rImageInfo->m_miplevels);

        TextureFileHeader::Format format(rImageInfo->m_format);
        uint32_t nBytesPerPixel = BitsPerPixel(format) * ComponentCount(format) / 8;

        std::vector<char> rFrame;

        for (uint32_t mip = 0; mip < rImageInfo->m_miplevels; ++mip)
        {
            uint32_t imageWidth = rImageInfo->m_width >> mip;
            uint32_t imageHeight = rImageInfo->m_height >> mip;

            if (imageWidth == 0) imageWidth = 1;
            if (imageHeight == 0) imageHeight = 1;

            uint32_t totalBytesToRead = imageWidth * imageHeight * rImageInfo->m_depth * nBytesPerPixel;
            uint32_t offset = fileHeader.m_mipOffset[mip];
            file.seekg((relativeOffsets ? startOffset : 0) + offset);

            rFrame = std::vector<char>(totalBytesToRead);

            if (!file.read(rFrame.data(), totalBytesToRead))
            {
                __debugbreak();
            }

            rImageInfo->m_data.push_back(rFrame);
            rImageInfo->m_dataSize.push_back(totalBytesToRead);
        }

        return rImageInfo;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////

    std::shared_ptr<TextureFileReader::ImageInfo>  TextureFileReader::Read()
    {
        std::ifstream is;
        is.open(m_inFileName, std::ios::binary);
        return Read(is, 0, false);
    }

    inline bgfx::TextureFormat::Enum ToBgfxFormat(TextureFileHeader::Format format)
    {
        switch (format)
        {
        case TextureFileHeader::Format::eRGBA32Bit:
            return bgfx::TextureFormat::Enum::RGBA8;
        case TextureFileHeader::Format::eRGBA64Bit:
            return bgfx::TextureFormat::Enum::RGBA16F;
        case TextureFileHeader::Format::eRGBA128Bit:
            return bgfx::TextureFormat::Enum::RGBA32F;
        default:
            __debugbreak();
            return bgfx::TextureFormat::Enum::RGBA8;
        }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////

    std::shared_ptr<TextureFileReader::ImageInfo> TextureFileReader::ReadHeader(
        std::ifstream& file, uint32_t startOffset, TextureFileHeader& fileHeader)
    {
        ::memset(&fileHeader, 0, sizeof(fileHeader));

        const uint32_t headerSize = sizeof(fileHeader);
        uint32_t currentOffset = startOffset;
        file.seekg(currentOffset);

        uint32_t totalBytesRead = 0;

        while (totalBytesRead != headerSize)
        {
            if (file.read((char*)&fileHeader + totalBytesRead, headerSize))
            {
                totalBytesRead += headerSize;
            }
            else
            {
                __debugbreak();
            }
        }

        if (::strncmp(fileHeader.m_fileType, WXTEXTUREFILE_MAGICID, 4))
        {
            __debugbreak();
        }

        if (fileHeader.m_version < 3)
        {
            if (strncmp(fileHeader.m_fileType2, WXTEXTUREFILE_MAGICID, 4))
            {
                __debugbreak();
            }
        }

        if (fileHeader.m_version < 4)
        {
            fileHeader.m_hasTranslucentPixels = false;
        }

        if (fileHeader.m_version < 5)
        {
            fileHeader.m_origDimensions[0] = fileHeader.m_textureDimensions[0];
            fileHeader.m_origDimensions[1] = fileHeader.m_textureDimensions[1];
        }

        if (fileHeader.m_version < 6)
        {
            fileHeader.m_depth = 1;
        }

        fileHeader.m_depth = std::max<uint32_t>(fileHeader.m_depth, 1);

        uint32_t width = fileHeader.m_textureDimensions[0];
        uint32_t height = fileHeader.m_textureDimensions[1];
        uint32_t mipLevels = fileHeader.m_miplevels;

        if (width <= 0 || height <= 0 || mipLevels <= 0)
        {
            __debugbreak();
        }

        std::shared_ptr<TextureFileReader::ImageInfo> rInfo(std::make_shared<TextureFileReader::ImageInfo>());
        rInfo->m_version = fileHeader.m_version;
        rInfo->m_width = fileHeader.m_textureDimensions[0];
        rInfo->m_height = fileHeader.m_textureDimensions[1];
        rInfo->m_miplevels = fileHeader.m_miplevels;
        rInfo->m_format = fileHeader.m_pixelFormat;
        rInfo->m_hasTranslucentPixels = fileHeader.m_hasTranslucentPixels;
        rInfo->m_origWidth = fileHeader.m_origDimensions[0];
        rInfo->m_origHeight = fileHeader.m_origDimensions[1];
        rInfo->m_depth = fileHeader.m_depth;

        return rInfo;
    }

    bgfx::TextureHandle TextureFileReader::LoadTexture()
    {
        auto image = Read();
        const bgfx::Memory* mem = bgfx::alloc(
            image->m_data[0].size());
        memcpy(mem->data, image->m_data[0].data(), image->m_data[0].size());
        if (image->m_depth > 1)
        {
            bgfxh<bgfx::TextureHandle> outTex = bgfx::createTexture3D(
                uint16_t(image->m_width)
                , uint16_t(image->m_height)
                , uint16_t(image->m_depth)
                , 1 < image->m_miplevels
                , ToBgfxFormat(image->m_format)
                , 0
                , mem
            );

            return outTex;
        }
        else
        {
            bgfxh<bgfx::TextureHandle> outTex = bgfx::createTexture2D(
                uint16_t(image->m_width)
                , uint16_t(image->m_height)
                , 1 < image->m_miplevels
                , 1
                , ToBgfxFormat(image->m_format)
                , 0
                , mem
            );

            return outTex;
        }
    }
}