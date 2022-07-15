#pragma once

namespace bgfx
{
    struct Memory;
    const Memory* compileShader(const std::string& shader, char shadertype);
}