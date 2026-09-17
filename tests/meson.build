test_inc = include_directories('../src')

gtest_dep = dependency('gtest', fallback: 'gtest', required: true)
gtest_main_dep = dependency('gtest_main', required: true)

element_test_src = [
    'element_test.cc',
    'integration_test.cc',
    'pipeline_test.cc',
    'logger.cc'
]

element_test_exe = executable(
    'element_test_exe',
    sources: element_test_src,
    dependencies: [gtest_dep, gtest_main_dep, gstpp_dep],
    include_directories: [test_inc],
)

test('elements', element_test_exe, 
     args: ['--gtest_filter=ElementTest.*'],
     suite: 'elements')

test('pipeline', element_test_exe, 
     args: ['--gtest_filter=PipelineTest.*'],
     suite: 'pipelines')

test('integration', element_test_exe, 
     args: ['--gtest_filter=IntegrationTest.*'],
     suite: 'integration')