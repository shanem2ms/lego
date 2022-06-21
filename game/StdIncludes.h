#pragma once
#include <string>
#include <vector>
#include <map>
#include <memory>
#include <sstream>
#include <fstream>

#include <bgfx/bgfx.h>
#include <gmtl/gmtl.h>
#include <gmtl/Point.h>
#include <gmtl/AABox.h>
#include <gmtl/AABoxOps.h>
#include <gmtl/Matrix.h>
#include <gmtl/Quat.h>
#include <gmtl/Vec.h>
#include <gmtl/Plane.h>
#include <gmtl/PlaneOps.h>
#include <gmtl/Frustum.h>

#ifdef SAM_COROUTINES
#include <coroutine>

#include <cppcoro/sync_wait.hpp>
#include <cppcoro/task.hpp>
#include <cppcoro/static_thread_pool.hpp>
#include <cppcoro/when_all.hpp>

extern cppcoro::static_thread_pool g_threadPool;


namespace cppcoro
{
    template <typename Func>
    task<> dispatch(Func func) {
        co_await g_threadPool.schedule();
        co_await func();
    }
}

namespace co = cppcoro; 
#endif

template <class T> class bgfxh
{
    T t;
public:
    bgfxh() :
        t(BGFX_INVALID_HANDLE)
    {

    }

    bgfxh(bgfxh<T>&& _t)
    {
        t = _t.t;
        _t.t = BGFX_INVALID_HANDLE;
    }

    bgfxh(T&& _t)
    {
        t = _t;
        _t = BGFX_INVALID_HANDLE;
    }

    operator T() const
    {
        return t;
    }

    T &operator = (const T& rhs)
    {
        free();
        t = rhs;
        return t;
    }

    void free()
    {
        if (bgfx::isValid(t))
        {
            bgfx::destroy(t);
            t = BGFX_INVALID_HANDLE;
        }
    }
    ~bgfxh()
    {
        free();
    }

    bool isValid() const
    {
        return bgfx::isValid(t);
    }
private:
    bgfxh(const bgfxh& rhs);

    bgfxh<T>& operator = (const bgfxh<T>& rhs);
};

typedef unsigned char byte;



