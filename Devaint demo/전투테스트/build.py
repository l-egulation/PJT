"""Locate the shared Unity launch guide without rebuilding obsolete combat art."""
from pathlib import Path
HERE=Path(__file__).resolve().parent

def main():
    guide=HERE/'template.html'
    assert guide.is_file(), guide
    print('Common Unity launch guide:', guide)

if __name__=='__main__':
    main()
