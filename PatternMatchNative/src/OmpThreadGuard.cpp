#include "OmpThreadGuard.h"
PatternMatch::OmpThreadGuard::OmpThreadGuard(int new_threads)
{
    prev_threads_ = omp_get_max_threads();
    omp_set_num_threads(std::max(1, new_threads));
}
PatternMatch::OmpThreadGuard::~OmpThreadGuard()
{
    omp_set_num_threads(prev_threads_);
}