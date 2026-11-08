#include <stdio.h>
#include "ijc_native.h"

static int failures = 0;

static void expect(const char *label, int32_t actual, int32_t expected)
{
    if (actual != expected)
    {
        printf("FAIL %s: expected %d, got %d\n", label, (int)expected, (int)actual);
        failures++;
    }
    else
    {
        printf("ok   %s = %d\n", label, (int)actual);
    }
}

int main(void)
{
    expect("score 90, tentacles 6  (50000+22500+9000+10000)", ijc_placement_fee_cents(90, 6), 91500);
    expect("score 100, tentacles 0 (50000+25000+0+10000)",    ijc_placement_fee_cents(100, 0), 85000);
    expect("score 65, tentacles 10 (50000+16250+15000)",      ijc_placement_fee_cents(65, 10), 81250);
    expect("score 89, tentacles 0  (no premium)",             ijc_placement_fee_cents(89, 0), 72250);
    expect("score 0, tentacles 0   (base fee)",               ijc_placement_fee_cents(0, 0), 50000);
    expect("score 100, tentacles 64 (maximum)",               ijc_placement_fee_cents(100, 64), 181000);
    expect("score -1  (invalid)",                             ijc_placement_fee_cents(-1, 0), -1);
    expect("score 101 (invalid)",                             ijc_placement_fee_cents(101, 0), -1);
    expect("tentacles -1 (invalid)",                          ijc_placement_fee_cents(50, -1), -1);
    expect("tentacles 65 (invalid)",                          ijc_placement_fee_cents(50, 65), -1);
    return failures == 0 ? 0 : 1;
}
