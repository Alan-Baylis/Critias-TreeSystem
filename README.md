Critias Tree System

Would you like to see how it looks in a real game? Check out ['The Unwritten Critias'](http://www.unwrittencritias.com), our upcoming game.

![alt tag](https://github.com/AssemblyJohn/Critias-TreeSystem/blob/master/CritiasTree.png)

The Critias tree system is an Unity addition that is used for an, currently in-development, open-world quest named 'The Unwritten Critias'. It was developed due to the poor
performance of the Unity's built-in SpeedTree implementation.

Features:
- Support for 100k+ tress with collisions
- Painting trees on non-terrains
- Being able to completely remove the terrain after editing the trees
- Support for multiple tree types
- CullingGroup API for cells
- Instancing support
- Tillable terrain support
- Billboard rotation
- Tree Y rotation
- Tree wind
- LOD System
- Animated SpeedTree style CrossFade
- Uber-fast billboard system with 400+ FPS at 200k+ trees

! IMPORTANT !
At runtime the managed terain's trees and foliage is turned off, so your terrain's grass will go away. In order to mitigate that I would recommend having two terrains, one for trees and the other for grass. A script, 'TerrainUtility' for that usage has been added.

HOWTO:
Long story short, there are two important scripts. 'Treeifier' and 'TreeSystem'. Create an game object and add it these two scripts. The 'Treeifier' script is used for extracting the data required for each drawn tree type and generating the indexed and optimized data at edit-time. The 'TreeSystem' script is used for using the data that was generated at edit-time and rendering it in a very optimal manner.

For more details see the documentation. Is it quite difficult to understand, or you just don't like to read? No probs, just check out the example...

For any extra details, suggestions or questions and plans for the future, feel free to get in touch via the forum!

Forums link: https://forum.unity3d.com/threads/free-critias-speedtree-tree-system.437520/

Asset store link: - pending -

Is the system working great for you? Would you like to also have a detail or grass system in the future? Then consider donating! But there is absolutely no obligation for it, of course! It would just make me a little more motivated!

[![](https://www.paypalobjects.com/en_US/i/btn/btn_donateCC_LG.gif)](https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=MBCS7Q244JQUS)
