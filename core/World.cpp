#include "StdIncludes.h"
#include "World.h"
#include "Application.h"
#include "Application.h"
#include "Engine.h"
#include <numeric>
#include "Mesh.h"
#include "OctTile.h"
#include "Frustum.h"
#include "gmtl/PlaneOps.h"
#include "LegoBrick.h"
#include "PartDefs.h"
#include "BrickMgr.h"
#include "Physics.h"
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
        m_inspectmode(false),
        m_pPickedBrick(nullptr),
        m_debugDraw(0)
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
    void World::MouseDown(float x, float y, int buttonId)
    {
        if (buttonId == 1 && m_pPickedBrick != nullptr)
        {
            Matrix44f mat = m_pPickedBrick->GetWorldMatrix();
            Brick *pBrick = m_pPickedBrick->GetBrick();
            Matrix44f wm = m_pPickedBrick->GetWorldMatrix();
            int connectorIdx = m_pPickedBrick->GetHighlightedConnector();
            if (connectorIdx >= 0)
            {
                auto& connector = pBrick->m_connectors[connectorIdx];
                
                Vec4f cwpos;
                xform(cwpos, wm, Vec4f(connector.pos,1));

                Brick* pMyBrick = BrickManager::Inst().GetBrick(m_rightHandPartInst.id);
                BrickManager::Inst().LoadConnectors(pBrick);
                for (auto& myconnect : pMyBrick->m_connectors)
                {
                    if (Connector::CanConnect(connector.type, myconnect.type))
                    {
                        PartInst pi = m_rightHandPartInst;
                        Vec3f pos = Vec3f(cwpos) - (myconnect.pos * BrickManager::Scale);
                        pi.pos = pos;

                        AABoxf cbox = pMyBrick->m_collisionBox;
                        cbox.mMin = cbox.mMin * BrickManager::Scale + pi.pos;
                        cbox.mMax = cbox.mMax * BrickManager::Scale + pi.pos;
                        if (m_octTileSelection.CanAddPart(pi, cbox))
                            m_octTileSelection.AddPartInst(pi);
                        break;
                    }
                }
            }
        }
        else if (buttonId == 0 && m_pPickedBrick != nullptr)
        {
            PartInst pi;
            pi.id = m_pPickedBrick->GetPartId();
            Matrix44f wm = m_pPickedBrick->GetWorldMatrix();
            Vec4f offset;
            xform(offset, wm, Vec4f(0, 0, 0, 1));
            pi.pos = Vec3f(offset);
            m_octTileSelection.RemovePart(pi);
        }
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

    void World::MouseDrag(float x, float y, int buttonId)
    {
      
    }

    void World::WheelScroll(float delta)
    {

    }


    void World::MouseUp(int buttonId)
    {        
    }
   
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
        float speed = 0.01f;
        switch (k)
        {
        case 'P':
            BrickManager::Inst().LoadPrimitives(
                BrickManager::Inst().GetBrick(m_rightHandPartInst.id));
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
            m_debugDraw = (m_debugDraw + 1) % 3;
            break;
        case 'I':
            m_inspectmode = !m_inspectmode;
            Engine::Inst().SetDbgCam(m_inspectmode);
            break;
        case 'N':
            partChange = 1;
            break;
        case 'L':
            partChange = -1;
            break;
        case 'Q':            
            m_rightHandPartInst.rot *= make<Quatf>(AxisAnglef(
                -Math::PI_OVER_2, Vec3f(1, 0, 0)));
            SetRightHandPart(m_rightHandPartInst);
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
        case 'L':
            partChange = 0;
            break;
        }
    }

    std::string g_partName;
    Loc g_hitLoc(0, 0, 0);
    float g_hitLocArea = 0;
    float g_hitDist = 0;
    void World::Update(Engine& e, DrawContext& ctx)
    {
        ctx.debugDraw = m_debugDraw;
        if (m_octTiles == nullptr)
        {
            m_octTiles = std::make_shared<SceneGroup>();
            e.Root()->AddItem(m_octTiles);
            m_frustum = std::make_shared<Frustum>();
            e.Root()->AddItem(m_frustum);
            m_playerGroup = std::make_shared<SceneGroup>();
            e.Root()->AddItem(m_playerGroup);
            Camera::Fly fly;
            Camera::Fly dfly;
            Level::PlayerData playerdata;
            PartInst righthandpart;
            if (m_level.GetPlayerData(playerdata))
            {
                fly.pos = playerdata.pos;
                fly.dir = playerdata.dir;
                m_flymode = playerdata.flymode;
                m_inspectmode = playerdata.inspect;
                Engine::Inst().SetDbgCam(m_inspectmode);
                dfly.pos = playerdata.inspectpos;
                dfly.dir = playerdata.inspectdir;
                righthandpart = playerdata.rightHandPart;
            }
            else
            {
                fly.pos = Point3f(0.0f, 0.0f, -0.5f);
                fly.dir = Vec2f(1.24564195f, -0.455399066f);
            }
            e.DrawCam().SetFly(dfly);
            e.ViewCam().SetFly(fly);

            m_rightHand = std::make_shared<SceneGroup>();
            m_rightHand->SetOffset(Vec3f(1.3f, -0.65f, 1.005f));
            m_rightHand->SetRotate(make<Quatf>(AxisAnglef(gmtl::Math::PI, 0.0f, 1.0f, 0.0f)) *
                make<Quatf>(AxisAnglef(-gmtl::Math::PI / 8.0f, 0.0f, 0.0f, 1.0f)) *
                make<Quatf>(AxisAnglef(gmtl::Math::PI / 8.0f, 1.0f, 0.0f, 0.0f)));
            m_rightHand->AddItem(std::make_shared<LegoBrick>("3820", 14));
            m_playerGroup->AddItem(m_rightHand);
            m_octTiles->BeforeDraw([this](DrawContext& ctx) { ctx.m_pgm = BGFX_INVALID_HANDLE; return true; });
            SetRightHandPart(righthandpart);
            m_physics = std::make_shared<Physics>();
        }
    
        ctx.m_physics = m_physics;
        if (ctx.m_physics)
            ctx.m_physics->Step(ctx);

        if (ctx.m_pickedItem != nullptr)
        {
            std::shared_ptr<LegoBrick> pBrick = 
                std::dynamic_pointer_cast<LegoBrick>(ctx.m_pickedItem);
            if (m_pPickedBrick != pBrick && 
                m_pPickedBrick != nullptr)
            {
                m_pPickedBrick->SetPickData(-1);
            }
            if (pBrick != nullptr)
            {
                pBrick->SetPickData(ctx.m_pickedVal);
            }

            m_pPickedBrick = pBrick;
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
            m_octTiles->Clear();
            m_octTileSelection.Update(e, ctx, playerbounds);
            m_octTileSelection.AddTilesToGroup(m_octTiles);
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


        m_playerGroup->SetOffset(newPos);
        m_playerGroup->SetRotate(fly.Quat());
        dfly.pos = newPos;
        dcam.SetFly(dfly);

        if ((ctx.m_frameIdx % 60) == 0)
        {
            Level::PlayerData playerdata;
            playerdata.pos = fly.pos;
            playerdata.dir = fly.dir;
            playerdata.flymode = m_flymode;
            playerdata.inspect = m_inspectmode;
            playerdata.inspectpos = dfly.pos;
            playerdata.inspectdir = dfly.dir;
            playerdata.rightHandPart = m_rightHandPartInst;
            
            m_level.WritePlayerData(playerdata);
        }
    }

    void World::SetRightHandPart(const PartInst& part)
    {
        if (m_rightHandPart != nullptr)
        {
            m_rightHand->RemoveItem(m_rightHandPart);
            m_rightHandPart = nullptr;
        }
        m_rightHandPartInst = part;
        if (!part.id.IsNull())
        {
            m_rightHandPart = std::make_shared<LegoBrick>(part.id, part.paletteIdx, LegoBrick::Physics::None, true);
            m_rightHandPart->SetOffset(part.pos);
            m_rightHandPart->SetRotate(part.rot);
            m_rightHand->AddItem(m_rightHandPart);
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
