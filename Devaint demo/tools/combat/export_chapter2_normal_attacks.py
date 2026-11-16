"""Compatibility entry point for the approved five-species export."""
import argparse
from export_chapter2_approved_normals import main
if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    main(parser.parse_args().check)
