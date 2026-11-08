#ifndef IJC_NATIVE_H
#define IJC_NATIVE_H

#include <stdint.h>

#if defined(_WIN32)
#  define IJC_EXPORT __declspec(dllexport)
#else
#  define IJC_EXPORT __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

#define IJC_MAX_SCORE 100
#define IJC_MAX_TENTACLES 64
#define IJC_INVALID_INPUT (-1)

IJC_EXPORT int32_t ijc_placement_fee_cents(int32_t score, int32_t tentacles);

#ifdef __cplusplus
}
#endif

#endif
