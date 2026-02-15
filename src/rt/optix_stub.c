/*
 *  optix_stub.c - CPU-only fallback symbols for builds without OptiX/CUDA.
 */

#include "ray.h"
#include "lookup.h"
#include "rcontrib.h"
#include "view.h"
#include "optix_rvu.h"

static void no_optix_error(void)
{
	error(USER, "GPU acceleration is unavailable in this build. Use -g-.");
}

void printRayTracingTime(const clock_t clock)
{
	(void)clock;
}

void computeOptix(const size_t width, const size_t height, const unsigned int imm_irrad, RAY *rays)
{
	(void)width; (void)height; (void)imm_irrad; (void)rays;
	no_optix_error();
}

void contribOptix(const size_t width, const size_t height, const size_t ray_count,
		const unsigned int imm_irrad, const unsigned int lim_dist,
		const unsigned int contrib, const unsigned int bins,
		double *rays, LUTAB *modifiers)
{
	(void)width; (void)height; (void)ray_count;
	(void)imm_irrad; (void)lim_dist; (void)contrib; (void)bins;
	(void)rays; (void)modifiers;
	no_optix_error();
}

void renderOptix(const VIEW *view, const size_t width, const size_t height,
		const double dstrpix, const double mblur, const double dblur,
		COLOR *colors, float *depths, void (*freport)(double))
{
	(void)view; (void)width; (void)height;
	(void)dstrpix; (void)mblur; (void)dblur;
	(void)colors; (void)depths; (void)freport;
	no_optix_error();
}

void renderOptixIterative(const VIEW *view, const int width, const int height, const int moved,
		void (*fpaint)(int, int, int, int, const unsigned char *),
		void (*fplot)(double *, int))
{
	(void)view; (void)width; (void)height; (void)moved;
	(void)fpaint; (void)fplot;
	no_optix_error();
}

void endOptix()
{
}
