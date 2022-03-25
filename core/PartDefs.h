#pragma once

namespace sam
{
    struct PartId
    {
        char _id[8];

        PartId() {
            memset(_id, 0, sizeof(_id));
        }

        PartId(const PartId& other)
        {
            memcpy(_id, other._id, sizeof(_id));
        }

        template <size_t T> PartId(const char(&str)[T])
        {
            static_assert(T <= (sizeof(_id) + 1));
            memset(_id, 0, sizeof(_id));
            memcpy(_id, str, std::min(T, sizeof(_id)));
        }
        bool IsNull() const
        {
            return _id[0] == 0;
        }
        PartId(const std::string& partfile);
        std::string GetFilename() const;
        std::string Name() const;
    };

    struct PartInst
    {
        PartId id;
        int paletteIdx;
        Vec3f pos;
        Quatf rot;
        bool connected;
        bool canBeDestroyed;
    };

    inline bool operator < (const PartId& lhs, const PartId& rhs)
    {
        return strncmp(lhs._id, rhs._id, sizeof(lhs._id)) < 0;
    }

    inline bool operator == (const PartId& lhs, const PartId& rhs)
    {
        return strncmp(lhs._id, rhs._id, sizeof(lhs._id)) == 0;
    }

    struct SlotPart
    {
        PartId id;
        int colorCode;
    };
}