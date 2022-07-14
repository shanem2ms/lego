#define WIN32_LEAN_AND_MEAN             // Exclude rarely-used stuff from Windows headers
#define NOMINMAX
#define WIN32_LEAN_AND_MEAN             // Exclude rarely-used stuff from Windows headers
// Windows Header Files
#include <shaderc.h>
#include <Windows.h>

namespace bgfx
{
    typedef bool (*CompileShaderFunc)(const char* _varying, const char* _comment, char* _shader, uint32_t _shaderLen, bgfx::Options& _options, bx::WriterI* _writer);

#define BGFX_SHADER_BIN_VERSION 11
#define BGFX_CHUNK_MAGIC_CSH BX_MAKEFOURCC('C', 'S', 'H', BGFX_SHADER_BIN_VERSION)
#define BGFX_CHUNK_MAGIC_FSH BX_MAKEFOURCC('F', 'S', 'H', BGFX_SHADER_BIN_VERSION)
#define BGFX_CHUNK_MAGIC_VSH BX_MAKEFOURCC('V', 'S', 'H', BGFX_SHADER_BIN_VERSION)

#define BGFX_SHADERC_VERSION_MAJOR 1
#define BGFX_SHADERC_VERSION_MINOR 18

    class File
    {
    public:
        File()
            : m_data(NULL)
            , m_size(0)
        {
        }

        ~File()
        {
            delete[] m_data;
        }

        void load(const bx::FilePath& _filePath)
        {
            bx::FileReader reader;
            if (bx::open(&reader, _filePath))
            {
                m_size = (uint32_t)bx::getSize(&reader);
                m_data = new char[m_size + 1];
                m_size = (uint32_t)bx::read(&reader, m_data, m_size, bx::ErrorAssert{});
                bx::close(&reader);

                if (m_data[0] == '\xef'
                    && m_data[1] == '\xbb'
                    && m_data[2] == '\xbf')
                {
                    bx::memMove(m_data, &m_data[3], m_size - 3);
                    m_size -= 3;
                }

                m_data[m_size] = '\0';
            }
        }

        const char* getData() const
        {
            return m_data;
        }

        uint32_t getSize() const
        {
            return m_size;
        }

    private:
        char* m_data;
        uint32_t m_size;
    };

    class StringWriter : public bx::WriterI
    {
    public:
        StringWriter()
        {}

        virtual int32_t write(const void* _data, int32_t _size, bx::Error*) override
        {
            const char* data = (const char*)_data;
            m_buffer.insert(m_buffer.end(), data, data + _size);
            return _size;
        }

    private:
        std::string m_buffer;
    };

    void help(const char* ptr = nullptr) {}
    bx::StringView baseName(const bx::StringView& _filePath)
    {
        bx::FilePath fp(_filePath);
        return bx::strFind(_filePath, fp.getBaseName());
    }
    bool g_verbose = false;

    Options::Options()
        : shaderType(' ')
        , disasm(false)
        , raw(false)
        , preprocessOnly(false)
        , depends(false)
        , debugInformation(false)
        , avoidFlowControl(false)
        , noPreshader(false)
        , partialPrecision(false)
        , preferFlowControl(false)
        , backwardsCompatibility(false)
        , warningsAreErrors(false)
        , keepIntermediate(false)
        , optimize(false)
        , optimizationLevel(3)
    {
    }

    int compileShader()
    {
        HMODULE hShaderDyn = LoadLibrary("shadercdyn.dll");
        CompileShaderFunc compileShader = (CompileShaderFunc)GetProcAddress(hShaderDyn, "compileShader");

        Options options;
        const char* filePath = "C:/homep4/lego/game/shaders/fs_pickbrick.sc";
        const char* varyingdef = "C:/homep4/lego/game/shaders/varying.def.sc";

        options.inputFilePath = filePath;
        options.shaderType = 'f';
        options.platform = "windows";
        options.profile = "ps_4_0";
        options.preprocessOnly = true;
     
        options.includeDirs.push_back("C:/homep4/lego/../install/x64-Debug/include/bgfx");

        std::string dir;
        {
            bx::FilePath fp(filePath);
            bx::StringView path(fp.getPath());

            dir.assign(path.getPtr(), path.getTerm());
            options.includeDirs.push_back(dir);
        }
       
        std::string commandLineComment = "// shaderc command line:\n";
        bool compiled = false;

        bx::FileReader reader;
        if (!bx::open(&reader, filePath))
        {
            bx::printf("Unable to open file '%s'.\n", filePath);
        }
        else
        {
            const char* varying = NULL;
            File attribdef;
            attribdef.load(varyingdef);
            options.dependencies.push_back(varyingdef);
            varying = attribdef.getData();

            const size_t padding = 16384;
            uint32_t size = (uint32_t)bx::getSize(&reader);
            char* data = new char[size + padding + 1];
            size = (uint32_t)bx::read(&reader, data, size, bx::ErrorAssert{});

            if (data[0] == '\xef'
                && data[1] == '\xbb'
                && data[2] == '\xbf')
            {
                bx::memMove(data, &data[3], size - 3);
                size -= 3;
            }

            // Compiler generates "error X3000: syntax error: unexpected end of file"
            // if input doesn't have empty line at EOF.
            data[size] = '\n';
            bx::memSet(&data[size + 1], 0, padding);
            bx::close(&reader);

            bx::WriterI* writer = new StringWriter();
            compiled = compileShader(varying, commandLineComment.c_str(), data, size, options, writer);

        }

        if (compiled)
        {
            return bx::kExitSuccess;
        }

        bx::printf("Failed to build shader.\n");
        return bx::kExitFailure;
    }

}