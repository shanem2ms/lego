#pragma once

namespace bgfx
{
    struct Memory;
}
namespace sam
{
    class ShaderCompiler
    {
        std::string m_varying;
    public:
        const bgfx::Memory* CompileShader(const std::string& shader, char shadertype);
    };
}

