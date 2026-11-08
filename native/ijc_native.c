#include "ijc_native.h"

#define IJC_BASE_FEE_CENTS 50000
#define IJC_CENTS_PER_SCORE_POINT 250
#define IJC_CENTS_PER_TENTACLE 1500
#define IJC_PREMIUM_THRESHOLD 90
#define IJC_PREMIUM_CENTS 10000

int32_t ijc_placement_fee_cents(int32_t score, int32_t tentacles)
{
    if (score < 0 || score > IJC_MAX_SCORE || tentacles < 0 || tentacles > IJC_MAX_TENTACLES)
    {
        return IJC_INVALID_INPUT;
    }

    int32_t fee = IJC_BASE_FEE_CENTS
                + IJC_CENTS_PER_SCORE_POINT * score
                + IJC_CENTS_PER_TENTACLE * tentacles;

    if (score >= IJC_PREMIUM_THRESHOLD)
    {
        fee += IJC_PREMIUM_CENTS;
    }

    return fee;
}
