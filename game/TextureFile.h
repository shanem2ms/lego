////////////////////////////////////////////////////////////////////////////////////////////////////
//  COPYRIGHT © 2006 by WSI Corporation
////////////////////////////////////////////////////////////////////////////////////////////////////
/// \author SMorriso
#pragma once

// Forward declarations.
#include <string>
#include <memory>
#include <vector>
#include <fstream>

namespace sam
{
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // TextureFile
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    // Current highest version
    static const uint32_t WXTEXTUREFILE_CURRENTVERSION = 6;
    static const uint32_t WXTEXTUREFILE_HIGHESTSUPPORTEDVERSION = 6;
    static const char    WXTEXTUREFILE_MAGICID[] = "vqtx";

#pragma pack(push, 1)
    struct  TextureFileHeader
    {

        enum Format {
            eInvalidFormat = 0,
            eGrey8Bit,      // One component, 8 bits/component
            eGrey16Bit,     // One component, 16 bits/component
            eGrey32Bit,     // One component, 32 bits/component
            eRGB24Bit,      // 3 components, 8 bits/component
            eRGB48Bit,      // 3 components, 16 bits/component
            eRGB96Bit,      // 3 components, 32 bits/component
            eRGBA32Bit,     // 4 components, 8 bits/component, wrapped as RGBA in a 32 bit integer.
            eARGB32Bit,     // 4 component, 8 bits/component, wrapped as ARGB in a 32 bit integer.
            eRGBA64Bit,     // 4 components, 16 bits/component
            eRGBA128Bit,    // 4 components, 32 bits/component
            eDXT5,          // 4 components, 1byte/sample
            e3DC,           // 2 component normal maps
            eR32F,          // 1 component, 32 bit float
            eG16R16,        // 1 component wxint of a hybrid of 16 bits shelf and 16 bits for height
            eIndex8Bit,     // color index
            eJPEG,          // 1 component, varying size <= 2.4-bits per pixel
            eG16R16M,		// G is shelf, R is 16 bit ramped value which uses float min/max vals
            eBGRA32Bit,		// When D3D says ARGB they really mean BGRA
            ePVR2bpp,		// PowerVR compressed texture, 2bpp
            ePVR4bpp,		// PowerVR compressed texture, 4bpp
            eASTC8x8,       // ASTC Compression, 2bpp
            eASTC4x4,       // ASTC Compression, 8bpp
            eYUV420,        // 3 components, planar, 16 bits/component
            eFormatCnt      // Always last, please
        };
        // NOTE:
        // Do not mess with the order of fields in this header.

        // TODO:
        // As we add more version we might want to split version-specific
        // information into sub-structs to make things easier so we can support
        // backwards compatibility.

        char    m_fileType[4];                  // should be "vqtx" for all texture files
        uint32_t m_version;                      // A version number.

        // Version 3
        Format m_pixelFormat;    // format of file.
        uint32_t m_textureDimensions[2];         // Width and height of the texture
        uint16_t m_miplevels;                    // number of mip-levels in texture
        uint64_t m_mipOffset[16];                // offset to the different mip-map levels
        uint32_t m_origFilePathSize;             // Size of origFilePath
        uint32_t m_origFilePath;                 // optional helper path to the original file (offset)
        char m_fileType2[4];                    // should be "vqtx" for all texture files
        bool m_preMultAlpha;                    // 'true' if alpha is premultiplied

        // Version 4 etc.
        bool m_hasTranslucentPixels;            // True if there are any translucent pixels in this movie

        // Version 5.
        uint32_t m_origDimensions[2];            // Original width/height before being resized

        // Version 6
        uint32_t m_depth;
    };

#pragma pack(pop)

    ////////////////////////////////////////////////////////////////////////////////////////////////////

    class TextureFileReader
    {
    public:
        static std::shared_ptr<TextureFileReader> Create(const std::string& inFileName);

        struct ImageInfo
        {
            uint32_t GetWidth()
            {
                return m_width;
            }

            uint32_t GetHeight()
            {
                return m_height;
            }

            uint32_t GetOrigWidth()
            {
                return m_origWidth;
            }

            uint32_t GetOrigHeight()
            {
                return m_origHeight;
            }

            uint32_t m_version;
            uint32_t m_width;
            uint32_t m_height;
            uint32_t m_origWidth;
            uint32_t m_origHeight;
            uint16_t m_miplevels;
            uint32_t m_depth;
            bool m_hasTranslucentPixels;
            TextureFileHeader::Format m_format;
            std::vector<std::vector<char>> m_data;
            std::vector<uint32_t> m_dataSize;
        };

        /// Throws the following exceptions:
        /// \exception WxFileWontOpen
        /// \exception WxSystemError
        std::shared_ptr<ImageInfo> ReadHeader(std::ifstream& fileHandle, uint32_t startOffset, TextureFileHeader& fileHeader);

        std::shared_ptr<ImageInfo> Read(
            std::ifstream& fileHandle, uint32_t offset, bool relativeOffsets = false);

        std::shared_ptr<TextureFileReader::ImageInfo> Read();
        TextureFileReader(const std::string& inFileName);

        bgfx::TextureHandle LoadTexture();
    protected:

    private:
        std::string m_inFileName;
    };
}