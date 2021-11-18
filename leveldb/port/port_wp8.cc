// LevelDB Copyright (c) 2011 The LevelDB Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file. See the AUTHORS file for names of contributors.
//
// See port_example.h for documentation for the following types/functions.

// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 
//  * Redistributions of source code must retain the above copyright
//    notice, this list of conditions and the following disclaimer.
//  * Redistributions in binary form must reproduce the above copyright
//    notice, this list of conditions and the following disclaimer in the
//    documentation and/or other materials provided with the distribution.
//  * Neither the name of the University of California, Berkeley nor the
//    names of its contributors may be used to endorse or promote products
//    derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE REGENTS AND CONTRIBUTORS ``AS IS'' AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE REGENTS AND CONTRIBUTORS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//

#include "port/port_winrt.h"

#ifdef MCPE_PLATFORM_WINDOWS_FAMILY
#include <windows.h>
#include <cassert>

#define ZLIB

#ifdef SNAPPY
#include <snappy/snappy.h>
#elif defined(ZLIB)
#include <zlib/zlib.h>
#endif

namespace leveldb {
	namespace port {

		CondVar::CondVar(Mutex* mu) :
			waiting_(0),
			mu_(mu),
			sem1_(::CreateSemaphoreExW(NULL, 0, 10000, NULL, 0, SEMAPHORE_MODIFY_STATE)),
			sem2_(::CreateSemaphoreExW(NULL, 0, 10000, NULL, 0, SEMAPHORE_MODIFY_STATE)) {
			assert(mu_);
		}

		CondVar::~CondVar() {
			::CloseHandle(sem1_);
			::CloseHandle(sem2_);
		}

		void CondVar::Wait() {
			mu_->AssertHeld();

			wait_mtx_.Lock();
			++waiting_;
			wait_mtx_.Unlock();

			mu_->Unlock();

			// initiate handshake
			::WaitForSingleObjectEx(sem1_, INFINITE, FALSE);
			::ReleaseSemaphore(sem2_, 1, NULL);
			mu_->Lock();
		}

		void CondVar::Signal() {
			wait_mtx_.Lock();
			if (waiting_ > 0) {
				--waiting_;

				// finalize handshake
				::ReleaseSemaphore(sem1_, 1, NULL);
				::WaitForSingleObjectEx(sem2_, INFINITE, FALSE);
			}
			wait_mtx_.Unlock();
		}

		void CondVar::SignalAll() {
			wait_mtx_.Lock();
			for (long i = 0; i < waiting_; ++i) {
				::ReleaseSemaphore(sem1_, 1, NULL);
				while (waiting_ > 0) {
					--waiting_;
					::WaitForSingleObjectEx(sem2_, INFINITE, FALSE);
				}
			}
			wait_mtx_.Unlock();
		}

		AtomicPointer::AtomicPointer(void* v) {
			Release_Store(v);
		}

		void* AtomicPointer::Acquire_Load() const {
			void * p = nullptr;
			InterlockedExchangePointer(&p, rep_);
			return p;
		}

		void AtomicPointer::Release_Store(void* v) {
			InterlockedExchangePointer(&rep_, v);
		}

		void* AtomicPointer::NoBarrier_Load() const {
			return rep_;
		}

		void AtomicPointer::NoBarrier_Store(void* v) {
			rep_ = v;
		}

	}
}
#endif