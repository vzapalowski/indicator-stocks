#!/usr/bin/env bash

dpkg-buildpackage -S -k10096CCA -I*.userprefs -Iobj -I.git* -Ibin -I*.sh -I*.png
