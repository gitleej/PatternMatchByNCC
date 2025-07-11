#pragma once
#ifndef _OMP_THREAD_GUARD_H
#define _OMP_THREAD_GUARD_H

#include <algorithm>
#include <omp.h>

namespace PatternMatch
{
    class OmpThreadGuard
    {
    public:
        explicit OmpThreadGuard(int new_threads);

        ~OmpThreadGuard();
        

    private:
        int prev_threads_;
    };
}

#endif  // !_OMP_THREAD_GUARD_H
