################################################################################
# Source groups
################################################################################
set(Header_Files
    "db/builder.h"
    "db/dbformat.h"
    "db/db_impl.h"
    "db/db_iter.h"
    "db/filename.h"
    "db/log_format.h"
    "db/log_reader.h"
    "db/log_writer.h"
    "db/memtable.h"
    "db/skiplist.h"
    "db/snapshot.h"
    "db/table_cache.h"
    "db/version_edit.h"
    "db/version_set.h"
    "db/write_batch_internal.h"
    "helpers/memenv/memenv.h"
    "include/leveldb/c.h"
    "include/leveldb/cache.h"
    "include/leveldb/comparator.h"
    "include/leveldb/compressor.h"
    "include/leveldb/db.h"
    "include/leveldb/decompress_allocator.h"
    "include/leveldb/dumpfile.h"
    "include/leveldb/env.h"
    "include/leveldb/filter_policy.h"
    "include/leveldb/iterator.h"
    "include/leveldb/options.h"
    "include/leveldb/slice.h"
    "include/leveldb/snappy_compressor.h"
    "include/leveldb/status.h"
    "include/leveldb/table.h"
    "include/leveldb/table_builder.h"
    "include/leveldb/write_batch.h"
    "include/leveldb/zlib_compressor.h"
    "port/atomic_pointer.h"
    "port/port.h"
    "port/port_win.h"
    "port/thread_annotations.h"
    "port/win/stdint.h"
    "table/block.h"
    "table/block_builder.h"
    "table/filter_block.h"
    "table/format.h"
    "table/iterator_wrapper.h"
    "table/merger.h"
    "table/two_level_iterator.h"
    "util/arena.h"
    "util/coding.h"
    "util/crc32c.h"
    "util/Filepath.h"
    "util/hash.h"
    "util/histogram.h"
    "util/logging.h"
    "util/mutexlock.h"
    "util/random.h"
    "util/win_logger.h"
)  

source_group("Header Files" FILES ${Header_Files})

set(Source_Files
    "db/builder.cc"
    "db/c.cc"
    "db/dbformat.cc"
    "db/db_impl.cc"
    "db/db_iter.cc"
    "db/dumpfile.cc"
    "db/filename.cc"
    "db/leveldbutil.cc"
    "db/log_reader.cc"
    "db/log_writer.cc"
    "db/memtable.cc"
    "db/repair.cc"
    "db/snappy_compressor.cc"
    "db/table_cache.cc"
    "db/version_edit.cc"
    "db/version_set.cc"
    "db/write_batch.cc"
    "db/zlib_compressor.cc"
    "helpers/memenv/memenv.cc"
    "table/block.cc"
    "table/block_builder.cc"
    "table/filter_block.cc"
    "table/format.cc"
    "table/iterator.cc"
    "table/merger.cc"
    "table/table.cc"
    "table/table_builder.cc"
    "table/two_level_iterator.cc"
    "util/arena.cc"
    "util/bloom.cc"
    "util/cache.cc"
    "util/coding.cc"
    "util/comparator.cc"
    "util/crc32c.cc"
    "util/env.cc"
    "util/filter_policy.cc"
    "util/hash.cc"
    "util/histogram.cc"
    "util/logging.cc"
    "util/options.cc"
    "util/status.cc"
    "port/port_posix_sse.cc"
    )

if (WIN32)

set (Platform_Files 
    "port/port_win.cc"
    "util/env_win.cc"
    "util/win_logger.cc"
)
else ()
set (Platform_Files 
    "port/port_posix.cc"
    "util/env_posix.cc"
    "util/win_logger.cc"
)
endif()
    
source_group("Source Files" FILES ${Source_Files} ${Platform_Files})

set(ALL_FILES
    
    ${Header_Files} 
    ${Source_Files}
    ${Platform_Files}
)

add_library(leveldb STATIC ${ALL_FILES})


if (MSVC)
    set (PlatformIncludeDirs 
        "${CMAKE_CURRENT_SOURCE_DIR}/include/unistd"
    )
else()
    set (PlatformIncludeDirs )
endif()

target_include_directories(leveldb PUBLIC
    "${CMAKE_CURRENT_SOURCE_DIR}/."
    "${CMAKE_CURRENT_SOURCE_DIR}/include"
    "${CMAKE_CURRENT_SOURCE_DIR}/../"
    "${CMAKE_CURRENT_SOURCE_DIR}/../zlib"
    "${CMAKE_BINARY_DIR}/zlib"
    ${PlatformIncludeDirs}

    )

if (MSVC)
add_compile_definitions(DLLX=;WIN32;OS_WIN;_WINDLL;ssize_t=__int64)
else ()
add_compile_definitions(DLLX=;LEVELDB_PLATFORM_POSIX;OS_MACOSX)
endif ()   