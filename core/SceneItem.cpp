#include "StdIncludes.h"
#include "SceneItem.h"
#include <bgfx/bgfx.h>
#include <gmtl/FrustumOps.h>
#include "Mesh.h"

using namespace gmtl;
namespace sam
{
    SceneItem::SceneItem() :
        m_offset(0, 0, 0),
        m_scale(1, 1, 1),
        m_rotate(),
        m_enabled(true),
        m_isInitialized(false),
        m_pParent(nullptr)
    {
    }

    void SceneItem::DoDraw(DrawContext& ctx)
    {
        if (!m_enabled)
            return;
        if (!m_isInitialized)
        {
            Initialize(ctx);
            m_isInitialized = true;
        }
        Draw(ctx);
    }

    Matrix44f SceneItem::CalcMat() const
    {
        return makeTrans<Matrix44f>(m_offset) *
            makeRot<Matrix44f>(m_rotate) *
            makeScale<Matrix44f>(m_scale);
    }

    Matrix44f SceneItem::GetWorldMatrix() const
    {
        if (m_pParent == nullptr)
            return CalcMat();
        else
            return m_pParent->GetWorldMatrix() *
                CalcMat();
    }

    inline void AABoxAdd(AABoxf& aab1, const AABoxf& aab2)
    {
        if (aab1.isEmpty())
        {
            aab1 = aab2;
        }
        else
        {
            {
                const Point3f& min = aab1.getMin();
                const Point3f& pt = aab2.getMin();
                aab1.setMin(Point3f(pt[0] < min[0] ? pt[0] : min[0],
                    pt[1] < min[1] ? pt[1] : min[1],
                    pt[2] < min[2] ? pt[2] : min[2]));
            }

            {
                const Point3f& pt = aab2.getMax();
                const Point3f& max = aab1.getMax();
                aab1.setMax(Point3f(pt[0] > max[0] ? pt[0] : max[0],
                    pt[1] > max[1] ? pt[1] : max[1],
                    pt[2] > max[2] ? pt[2] : max[2]));
            }
        }
    }

    void SceneGroup::Decomission(DrawContext& ctx)
    {
        for (auto item : m_sceneItems)
        {
            item->Decomission(ctx);
        }
    }

    AABoxf SceneGroup::GetBounds() const
    {
        AABoxf bnds;
        for (auto item : m_sceneItems)
        {
            AABoxAdd(bnds, item->GetBounds());
        }

        Point3f pt;
        Matrix44f m = CalcMat();
        xform(pt, m, bnds.getMin());
        bnds.setMin(pt);
        xform(pt, m, bnds.getMax());
        bnds.setMax(pt);
        return bnds;
    }

    void SceneGroup::Draw(DrawContext& ctx)
    {
        if (m_beforeDraw != nullptr)
        {
            if (!m_beforeDraw(ctx))
                return;
        }
        Matrix44f prevMat = ctx.m_mat;
        for (auto item : m_sceneItems)
        {
            ctx.m_mat *= CalcMat();
            item->DoDraw(ctx);
            ctx.m_mat = prevMat;
        }

        if (m_afterDraw != nullptr)
            m_afterDraw(ctx);
    }

    void SceneText::SetText(const std::string& text)
    {
        m_text = text;
    }

    void SceneText::SetFont(const std::string& fontname, float size)
    {
        m_font = fontname;
        m_size = size;
    }

    void SceneText::Draw(DrawContext& ctx)
    {
        Matrix44f m =
            ctx.m_mat * CalcMat();

        Point3f pt = makeTrans<Point3f>(m);
    }

    AABoxf SceneText::GetBounds() const
    {
        Matrix44f m = CalcMat();
        return AABoxf();
        //nvgTextBounds()
    }

    void SceneRect_DrawCube()
    {
        Cube::init();

        // Set vertex and index buffer.
        bgfx::setVertexBuffer(0, Cube::vbh);
        bgfx::setIndexBuffer(Cube::ibh);
        uint64_t state = 0
            | BGFX_STATE_WRITE_RGB
            | BGFX_STATE_WRITE_A
            | BGFX_STATE_WRITE_Z
            | BGFX_STATE_DEPTH_TEST_LESS
            | BGFX_STATE_CULL_CCW
            | BGFX_STATE_MSAA;
        // Set render states.
        bgfx::setState(state);
    }

    void SceneRect::Draw(DrawContext& ctx)
    {
        const int padding = 2;
        Matrix44f m =
            ctx.m_mat * CalcMat();

        SceneRect_DrawCube();

        Cube::init();
        bgfx::submit(0, ctx.m_pgm);
    }

    AABoxf SceneRect::GetBounds() const
    {
        Matrix44f m = CalcMat();
        return AABoxf();
        //nvgTextBounds()
    }

    static const int boardSizeW = 32;
    static const int boardSizeH = 48;

    Camera::Camera() :
        m_mode(0),
        m_near(0.002f),
        m_far(25.0f)
    {}


    Quatf Camera::Fly::Quat() const
    {
        return make<gmtl::Quatf>(AxisAnglef(dir[0], 0.0f, 1.0f, 0.0f)) *
            make<gmtl::Quatf>(AxisAnglef(dir[1], 1.0f, 0.0f, 0.0f));
    }

    void Camera::Fly::GetDirs(Vec3f &right, Vec3f &up, Vec3f &forward) const
    {
        forward = make<gmtl::Quatf>(AxisAnglef(dir[1], 1.0f, 0.0f, 0.0f)) * Vec3f(0, 0, 1);
        forward = make<gmtl::Quatf>(AxisAnglef(dir[0], 0.0f, 1.0f, 0.0f)) * forward;
        normalize(forward);

        cross(right, forward, Vec3f(0, -1, 0));
        normalize(right);
        cross(up, forward, right);
        normalize(up);
    }

    gmtl::Matrix44f Camera::GetPerspectiveMatrix(float near, float far) const
    {
        Matrix44f proj;
        setPerspective(proj, 60.0f, m_aspect, near, far);
        return proj;
    }

    void Camera::Update(int w, int h)
    {        
        float aspect = (float)w / (float)h;
        setPerspective(m_proj, 60.0f, aspect, m_near, m_far);
        m_aspect = aspect;
        
    }        
    
    const gmtl::Matrix44f& Camera::ViewMatrix() const 
    {
        if (m_viewdirty)
        {
            Matrix44f rot, off, scl, perp;
            if (m_mode == 0)
            {
                Vec3f rightDir, upDir, lookDir;
                m_fly.GetDirs(rightDir, upDir, lookDir);
                rot.set(
                    rightDir[0], rightDir[1], rightDir[2], 0,
                    upDir[0], upDir[1], upDir[2], 0,
                    lookDir[0], lookDir[1], lookDir[2], 0,
                    0, 0, 0, 1);

                transpose(rot);
                setTrans(off, m_fly.pos);
            }
            else
            {
                setRot(rot, AxisAnglef(Math::PI + m_lookat.tilt, 1.0f, 0.0f, 0.0f));
                Vec3f vec(0, 0, m_lookat.dist);
                Quatf q = make<gmtl::Quatf>(AxisAnglef(Math::PI + m_lookat.tilt, 1.0f, 0.0f, 0.0f));
                vec = q * vec;
                setTrans(off, m_lookat.pos + vec);

            }
            m_view = off * rot;
            m_view = invert(m_view);
            m_viewdirty = false;
        }
        return m_view;
    }


    Frustumf Camera::GetFrustum(float near, float far) const
    {
        Frustumf f(ViewMatrix(), GetPerspectiveMatrix(near, far));
        normalize(f);
        return f;
    }
}