#include "StdIncludes.h"
#include "SceneItem.h"
#include "Application.h"
#include "PartDefs.h"
#include "Mongo.h"
#include "BrickMgr.h"
#include "nlohmann/json.hpp"
#include "zip.h"
#include <bsoncxx/json.hpp>
#include <mongocxx/client.hpp>
#include <mongocxx/stdx.hpp>
#include <mongocxx/uri.hpp>
#include <mongocxx/instance.hpp>
#include <bsoncxx/builder/stream/helpers.hpp>
#include <bsoncxx/builder/stream/document.hpp>
#include <bsoncxx/builder/stream/array.hpp>

using namespace nlohmann;
using namespace gmtl;
using bsoncxx::builder::stream::close_array;
using bsoncxx::builder::stream::close_document;
using bsoncxx::builder::stream::document;
using bsoncxx::builder::stream::finalize;
using bsoncxx::builder::stream::open_array;
using bsoncxx::builder::stream::open_document;

namespace sam
{
    Mongo::Mongo(const std::string& svr) :
        m_server(svr)
    {
        mongocxx::instance instance{}; // This should be done only once.
        mongocxx::client client{ mongocxx::uri(m_server)};
        mongocxx::database db = client["Bricks"];
        mongocxx::collection coll = db["BrickUsers"];
    }
}