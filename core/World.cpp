#include "StdIncludes.h"
#include "World.h"
#include "Application.h"
#include "Engine.h"
#include <numeric>
#include "Mesh.h"
#include "OctTile.h"
#include "Frustum.h"
#include "gmtl/PlaneOps.h"
#include "LegoBrick.h"
#include "BrickMgr.h"
#define NOMINMAX


using namespace gmtl;
const char* ldrpath = "C:\\ldraw";

namespace sam
{     
    World::World() :
        m_width(-1),
        m_height(-1),
        m_currentTool(0),
        m_gravityVel(0),        
        m_flymode(true),
        m_inspectmode(false)       
    {        
    }  

    void World::Open(const std::string& path)
    {
        m_level.OpenDb(path);
    }


    class Touch
    {
        Point2f m_touch;
        Point2f m_lastDragPos;
        bool m_isInit;
        bool m_isDragging;
    public:

        Vec2f m_initCamDir;

        Touch() : m_isInit(false),
            m_isDragging(false) {}

        bool IsInit() const {
            return m_isInit;
        }
        void SetInitialPos(const Point2f& mouse)
        {
            m_touch = mouse;
            m_isInit = true;
        }

        bool IsDragging() const { return m_isDragging; }

        void SetDragPos(const Point2f& newTouchPt)
        {
            Vec2f dpt = (newTouchPt - m_touch);
            if (length(dpt) > 0.04)
                m_isDragging = true;
            m_lastDragPos = newTouchPt;
        }

        const Point2f& LastDragPos() const { return m_lastDragPos; }
        const Point2f& TouchPos() const { return m_touch; }        
    };
    //https://shanetest-cos-earth.s3.us-east.cloud-object-storage.appdomain.cloud/usa10m_whqt/Q0/L0/R0/C0
    //https://shanetest-cos-earth.s3.us-east.cloud-object-storage.appdomain.cloud/world9m_whqt/Q0/L0/R0/C0
    //https://shanetest-cos-earth.s3.us-east.cloud-object-storage.appdomain.cloud/world9m_whqt/Q1/L3/R1/Q1_L3_R1_C0.png

    bool cursormode = false;
    void World::TouchDown(float x, float y, int touchId)
    {
        m_activeTouch = std::make_shared<Touch>();
        float xb = x / m_width;
        float yb = y / m_height;

        Engine& e = Engine::Inst();
        Camera::Fly la = e.DrawCam().GetFly();
        
        m_activeTouch->m_initCamDir = la.dir;
        m_activeTouch->SetInitialPos(Point2f(xb, yb));
    }

    constexpr float pi_over_two = 3.14159265358979323846f * 0.5f;
    void World::RawMove(float dx, float dy)
    {
        Engine& e = Engine::Inst();
        Camera::Fly la = e.DrawCam().GetFly();
        la.dir[0] += dx;
        la.dir[1] -= dy;
        la.dir[1] = std::max(la.dir[1], -pi_over_two);
        e.DrawCam().SetFly(la);
    }

    void World::TouchDrag(float x, float y, int touchId)
    {
        if (m_activeTouch != nullptr)
        {
            float xb = x / m_width;
            float yb = y / m_height;

            m_activeTouch->SetDragPos(Point2f(xb, yb));

            Vec2f dragDiff = Point2f(xb, yb) - m_activeTouch->TouchPos();

            Engine& e = Engine::Inst();
            Camera::Fly la = e.DrawCam().GetFly();

            la.dir[0] = m_activeTouch->m_initCamDir[0] + dragDiff[0] * 2;
            la.dir[1] = m_activeTouch->m_initCamDir[1] + dragDiff[1] * 2;
            la.dir[1] = std::max(la.dir[1], -pi_over_two);
            e.DrawCam().SetFly(la);

        }
    }

    void World::TouchUp(int touchId)
    {        
        m_activeTouch = nullptr;
        m_tiltVel = 0;
    }
    extern bool g_showOctBoxes;
    
    static int curPartIdx = 0;
    static int prevPartIdx = -1;

    const int LeftShift = 16;
    const int SpaceBar = 32;
    const int AButton = 'A';
    const int DButton = 'D';
    const int WButton = 'W';
    const int SButton = 'S';
    const int FButton = 'F';
    bool isPaused = false;
    int partChange = 0;

    int g_maxTileLod = 8;
    void World::KeyDown(int k)
    {
        float speed = 0.001f;
        switch (k)
        {
        case 'P':
            isPaused = !isPaused;
            break;
        case LeftShift:
            m_camVel[1] -= speed;
            break;
        case SpaceBar:
            m_camVel[1] += speed;
            break;
        case AButton:
            m_camVel[0] -= speed;
            break;
        case DButton:
            m_camVel[0] += speed;
            break;
        case WButton:
            m_camVel[2] += speed;
            break;
        case SButton:
            m_camVel[2] -= speed;
            break;
        case FButton:
            m_flymode = !m_flymode;
            break;
        case 'B':
            g_showOctBoxes = !g_showOctBoxes;
            break;
        case 'I':
            m_inspectmode = !m_inspectmode;
            Engine::Inst().SetDbgCam(m_inspectmode);
            break;
        case 'N':
            partChange = 1;
            break;
        }
        if (k >= '1' && k <= '9')
        {
            g_maxTileLod = k - '0';
        }
    }

    void World::KeyUp(int k)
    {
        switch (k)
        {
        case LeftShift:
        case SpaceBar:
            m_camVel[1] = 0;
            break;
        case AButton:
        case DButton:
            m_camVel[0] = 0;
            break;
        case WButton:
        case SButton:
            m_camVel[2] = 0;
            break;
        case 'N':
            //partChange = 0;
            break;
        }
    }

    std::string g_partName;
    Loc g_hitLoc(0, 0, 0);
    float g_hitLocArea = 0;
    float g_hitDist = 0;
    void World::Update(Engine& e, DrawContext& ctx)
    {
        if (m_worldGroup == nullptr)
        {
            m_brickManager = std::make_shared<BrickManager>("c:\\ldraw");
            m_worldGroup = std::make_shared<SceneGroup>();
            e.Root()->AddItem(m_worldGroup);
            m_targetCube = std::make_shared<TargetCube>();
            e.Root()->AddItem(m_targetCube);
            m_frustum = std::make_shared<Frustum>();
            e.Root()->AddItem(m_frustum);
            Camera::Fly fly;
            Camera::Fly dfly;
            Level::CamPos campos;
            if (m_level.GetCameraPos(campos))
            {
                fly.pos = campos.pos;
                fly.dir = campos.dir;
                m_flymode = campos.flymode;
                m_inspectmode = campos.inspect;
                Engine::Inst().SetDbgCam(m_inspectmode);
                dfly.pos = campos.inspectpos;
                dfly.dir = campos.inspectdir;
            }
            else
            {
                fly.pos = Point3f(0.0f, 0.0f, -0.5f);
                fly.dir = Vec2f(1.24564195f, -0.455399066f);
            }
            e.DrawCam().SetFly(dfly);
            e.ViewCam().SetFly(fly);

            m_shader = e.LoadShader("vs_cubes.bin", "fs_cubes.bin");
            m_worldGroup->BeforeDraw([this](DrawContext& ctx) { ctx.m_pgm = m_shader; return true; });


        }
        curPartIdx += partChange;
        if (curPartIdx != prevPartIdx)
        {
            if (m_legoBrick != nullptr)
                e.Root()->RemoveItem(m_legoBrick);
            std::string partname = m_brickManager->PartName(curPartIdx);
            m_legoBrick = std::make_shared<LegoBrick>(m_brickManager, partname);
            e.Root()->AddItem(m_legoBrick);

            g_partName = partname;
            prevPartIdx = curPartIdx;
        }

        m_frustum->SetEnabled(m_inspectmode);
       
        auto &cam = e.ViewCam();
        Camera::Fly fly = cam.GetFly();
        const float playerHeadHeight = 0.01f;
        const float playerBodyWidth = playerHeadHeight;

        Vec3f boundsExt(0.01f, 0.01f, 0.01f);
        AABoxf playerbounds(fly.pos - boundsExt, fly.pos + boundsExt);

        if (!isPaused)
        {
            m_worldGroup->Clear();
            m_octTileSelection.Update(e, ctx, playerbounds);
            m_octTileSelection.AddTilesToGroup(m_worldGroup);
        }

        std::shared_ptr<OctTile> tile = m_octTileSelection.TileFromPos(fly.pos);
        m_octTileSelection.GetNearFarMidDist(ctx.m_nearfar);
        e.ViewCam().SetNearFar(ctx.m_nearfar[0], ctx.m_nearfar[2]);

        {
            Matrix44f mat0 = cam.GetPerspectiveMatrix(ctx.m_nearfar[0], ctx.m_nearfar[1]) *
                cam.ViewMatrix();
            invert(mat0);
            Vec4f nearWsPt, farWsPt;
            xform(nearWsPt, mat0, Vec4f(0, 0, 0, 1));
            Matrix44f mat1 = cam.GetPerspectiveMatrix(ctx.m_nearfar[1], ctx.m_nearfar[2]) *
                cam.ViewMatrix();
            invert(mat1);
            xform(farWsPt, mat1, Vec4f(0, 0, 1, 1));
            nearWsPt /= nearWsPt[3];
            farWsPt /= farWsPt[3];
            Vec3f dir = Point3f(farWsPt[0], farWsPt[1], farWsPt[2]) -
                Point3f(nearWsPt[0], nearWsPt[1], nearWsPt[2]);
            normalize(dir);
            Loc hitLoc(0,0,0);
            Vec3i hitpt;

            if (m_octTileSelection.Intersects(Point3f(nearWsPt[0], nearWsPt[1], nearWsPt[2]), dir, hitLoc, hitpt))
            {
                AABox aabb = hitLoc.GetBBox();
                Vec3f extents = (aabb.mMax - aabb.mMin);
                const int tsz = 256;
                float scl = extents[0] / (float)tsz; 
                
                Point3f offset = Vec3f(hitpt[0], hitpt[1], hitpt[2]) * scl + aabb.mMin;
                m_targetCube->SetOffset(offset);
                m_targetCube->SetScale(Vec3f(scl, scl, scl));
                g_hitLoc = hitLoc;
                bool test;
                Matrix44f vp = cam.GetPerspectiveMatrix(ctx.m_nearfar[0], ctx.m_nearfar[1])*
                    cam.ViewMatrix();

                g_hitDist = dot(offset - fly.pos, dir);
            }
        }

        auto& dcam = e.DrawCam();
        Vec3f right, up, forward;
        Vec3f upworld(0, 1, 0);
        auto dfly = dcam.GetFly();
        dfly.GetDirs(right, up, forward);
        Vec3f fwWorld;
        cross(fwWorld, right, upworld);

        float flyspeedup = 1;
        if (m_flymode) flyspeedup *= 10;
        if (m_inspectmode) flyspeedup *= 50;

        Point3f newPos = dfly.pos + m_camVel[0] * right * flyspeedup +
            (m_camVel[1] + m_gravityVel) * upworld * flyspeedup +
            m_camVel[2] * fwWorld * flyspeedup;

        if (!m_inspectmode)
        {
            std::shared_ptr<OctTile> tile = m_octTileSelection.TileFromPos(fly.pos);
            if (tile == nullptr || tile->GetReadyState() < 3)
            {
                m_gravityVel = 0;
            }
            else
            {                
                AABoxf playerbox(Point3f(-playerBodyWidth, -playerHeadHeight * 2, -playerBodyWidth),
                    Point3f(playerBodyWidth, 0, playerBodyWidth));
                Vec3f normal;
                bool collision = tile->IsCollided(dfly.pos, newPos, playerbox, normal);
                if (!collision && !m_flymode)
                {
                    m_gravityVel -= 0.0005f;
                }
                else
                {
                    //fly.pos[1] = grnd + headheight;
                    m_gravityVel = 0;
                }
            }
        }
        else
        {
            m_gravityVel = 0;
        }


        dfly.pos = newPos;
        dcam.SetFly(dfly);

        if ((ctx.m_frameIdx % 60) == 0)
        {
            Level::CamPos campos;
            campos.pos = fly.pos;
            campos.dir = fly.dir;
            campos.flymode = m_flymode;
            campos.inspect = m_inspectmode;
            campos.inspectpos = dfly.pos;
            campos.inspectdir = dfly.dir;
            m_level.WriteCameraPos(campos);
        }
    }

    void World::Layout(int w, int h)
    {
        m_width = w;
        m_height = h;
    }
    
    struct Palette
    {
        float v;
        unsigned char r;
        unsigned char g;
        unsigned char b;

        Palette(float _V, unsigned char _r, unsigned char _g, unsigned char _b) :
            v(_V),
            r(_r),
            g(_g),
            b(_b)
        {  }
    };


    

    World::~World()
    {

    }

}
